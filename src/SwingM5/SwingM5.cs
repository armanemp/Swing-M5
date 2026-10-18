using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, IsOverlay = true, AutoRescale = false)]
    public class SwingM5 : Indicator
    {
        [Parameter("Left-side swing depth", DefaultValue = 2, MinValue = 2, MaxValue = 10, Group = "Swing detection")]
        public int PivotDepth { get; set; }

        [Parameter("ATR period", DefaultValue = 14, MinValue = 5, MaxValue = 100, Group = "Swing detection")]
        public int AtrPeriod { get; set; }

        [Parameter("Minimum swing size (ATR)", DefaultValue = 0.8, MinValue = 0.1, MaxValue = 5.0, Group = "Swing detection")]
        public double MinimumSwingAtr { get; set; }

        [Parameter("Swing measurement lookback (bars)", DefaultValue = 8, MinValue = 3, MaxValue = 30, Group = "Swing detection")]
        public int SwingMeasurementLookback { get; set; }

        [Parameter("Minimum reversal distance (ATR)", DefaultValue = 0.18, MinValue = 0.05, MaxValue = 1.0, Group = "Timing filter")]
        public double MinimumReversalAtr { get; set; }

        [Parameter("Maximum entry distance (ATR)", DefaultValue = 0.85, MinValue = 0.2, MaxValue = 3.0, Group = "Timing filter")]
        public double MaximumEntryDistanceAtr { get; set; }

        [Parameter("Minimum score", DefaultValue = 60, MinValue = 0, MaxValue = 100, Group = "Quality filters")]
        public int MinimumScore { get; set; }

        [Parameter("Use M15 context", DefaultValue = true, Group = "Multi-timeframe")]
        public bool UseM15Context { get; set; }

        [Parameter("Use M1 confirmation", DefaultValue = true, Group = "Multi-timeframe")]
        public bool UseM1Confirmation { get; set; }

        [Parameter("Sound alert on entry confirmation", DefaultValue = true, Group = "Alerts")]
        public bool SoundAlertOnEntryConfirmation { get; set; }

        [Parameter("Show swing markers", DefaultValue = false, Group = "Display")]
        public bool ShowSwingMarkers { get; set; }

        [Parameter("Show entry score labels", DefaultValue = false, Group = "Display")]
        public bool ShowScoreLabels { get; set; }

        [Parameter("Maximum displayed swings", DefaultValue = 80, MinValue = 10, MaxValue = 250, Group = "Display")]
        public int MaximumDisplayedSwings { get; set; }

        private Bars _m1Bars;
        private Bars _m15Bars;
        private readonly List<Pivot> _pivots = new List<Pivot>();
        private readonly List<Pivot> _visiblePivots = new List<Pivot>();
        private int _lastProcessedIndex = -1;
        private bool _timeframeWarningPrinted;

        protected override void Initialize()
        {
            _m1Bars = MarketData.GetBars(TimeFrame.Minute, SymbolName);
            _m15Bars = MarketData.GetBars(TimeFrame.Minute15, SymbolName);

            if (TimeFrame != TimeFrame.Minute5)
            {
                Print("Swing-M5 is designed for M5. Attach it to a five-minute chart to calculate swings.");
                _timeframeWarningPrinted = true;
            }
        }

        public override void Calculate(int index)
        {
            // The final bar is still forming. When Calculate is called for it,
            // process the previous (fully closed) bar instead.
            int decisionIndex = index == Bars.Count - 1 ? index - 1 : index;

            if (decisionIndex < 0 || decisionIndex <= _lastProcessedIndex)
                return;

            _lastProcessedIndex = decisionIndex;

            if (TimeFrame != TimeFrame.Minute5)
            {
                if (!_timeframeWarningPrinted)
                {
                    Print("Swing-M5 skipped calculation because the chart timeframe is not M5.");
                    _timeframeWarningPrinted = true;
                }

                return;
            }

            if (decisionIndex < AtrPeriod + PivotDepth + 5)
                return;

            // Evaluate the immediately previous closed M5 candle as the swing candidate.
            // One closed M5 candle must reject the candidate, followed by a closed M1
            // micro-structure break and a directional closed-M5 confirmation.
            int candidateIndex = decisionIndex - 1;
            if (candidateIndex < AtrPeriod + PivotDepth)
                return;

            bool isHigh = IsEarlyPivotHigh(Bars, candidateIndex, PivotDepth, decisionIndex);
            bool isLow = IsEarlyPivotLow(Bars, candidateIndex, PivotDepth, decisionIndex);

            // Ignore outside/ambiguous bars.
            if (isHigh == isLow)
                return;

            double candidatePrice = isHigh ? Bars.HighPrices[candidateIndex] : Bars.LowPrices[candidateIndex];
            if (GetRejectionWickRatio(candidateIndex, isHigh) < 0.28)
                return;

            if (!HasEntryTrigger(candidateIndex, decisionIndex, candidatePrice, isHigh))
                return;

            AddCandidate(candidateIndex, decisionIndex, isHigh);
        }

        private void AddCandidate(int candidateIndex, int decisionIndex, bool isHigh)
        {
            double atr = CalculateAtr(Bars, candidateIndex, AtrPeriod);
            if (atr <= 0 || double.IsNaN(atr) || double.IsInfinity(atr))
                return;

            double price = isHigh ? Bars.HighPrices[candidateIndex] : Bars.LowPrices[candidateIndex];
            DateTime pivotTime = Bars.OpenTimes[candidateIndex];

            Pivot previousSame = FindLastPivot(isHigh, pivotTime);
            Pivot previousOpposite = FindLastPivot(!isHigh, pivotTime);

            // Measure the recent local leg only. A distant prior entry can inflate this swing.
            double referencePrice = FindLocalOppositeExtreme(
                candidateIndex, isHigh, SwingMeasurementLookback, decisionIndex);

            double swingSize = Math.Abs(price - referencePrice);
            double sizeInAtr = swingSize / atr;
            double reversalDistance = isHigh
                ? price - Bars.ClosePrices[decisionIndex]
                : Bars.ClosePrices[decisionIndex] - price;
            double reversalInAtr = reversalDistance / atr;

            if (swingSize < atr * MinimumSwingAtr)
                return;

            // Avoid premature signals with almost no reversal and late entries after a chase.
            if (reversalInAtr < MinimumReversalAtr || reversalInAtr > MaximumEntryDistanceAtr)
                return;

            double displacement = CalculateDisplacement(candidateIndex, decisionIndex, price, isHigh);
            double displacementInAtr = displacement / atr;
            double rejectionWickRatio = GetRejectionWickRatio(candidateIndex, isHigh);
            bool structureShift = HasStructureShift(candidateIndex, decisionIndex, previousOpposite, isHigh);

            int htfBonus = UseM15Context
                ? CalculateHigherTimeframeBonus(price, isHigh, Bars.OpenTimes[decisionIndex].AddMinutes(5))
                : 0;

            // Hard reject entries against a strong M15 direction unless near a
            // confirmed same-side swing level.
            if (UseM15Context && htfBonus <= -15)
                return;

            int ltfBonus = UseM1Confirmation
                ? CalculateMicroConfirmationBonus(candidateIndex, decisionIndex, price, isHigh, atr)
                : 0;

            int score = 20;
            score += (int)Math.Round(Clamp((sizeInAtr - MinimumSwingAtr) * 6.0, 0, 12));
            score += (int)Math.Round(Clamp(displacementInAtr * 8.0, 0, 10));
            score += WickScore(rejectionWickRatio);
            score += ConfirmationCandleScore(decisionIndex, isHigh);
            if (structureShift)
                score += 6;
            score += htfBonus;
            score += ltfBonus;
            score += reversalInAtr <= 0.45 ? 8 : reversalInAtr <= 0.65 ? 4 : 0;
            score = (int)Clamp(score, 0, 100);

            if (score < MinimumScore)
                return;

            string structure = ClassifyStructure(isHigh, price, previousSame, atr);
            var pivot = new Pivot
            {
                Index = candidateIndex,
                Time = pivotTime,
                Price = price,
                IsHigh = isHigh,
                Score = score,
                Structure = structure,
                Atr = atr
            };

            _pivots.Add(pivot);
            DrawPivot(pivot, previousOpposite, decisionIndex);

            // Keep enough recent analytical history for structure classification without
            // retaining an unbounded list during very long sessions.
            int analyticalLimit = Math.Max(500, MaximumDisplayedSwings * 10);
            while (_pivots.Count > analyticalLimit)
                _pivots.RemoveAt(0);
        }


        private bool IsEarlyPivotHigh(Bars data, int center, int leftDepth, int availableThrough)
        {
            if (center - leftDepth < 0 || center + 1 > availableThrough)
                return false;

            double candidate = data.HighPrices[center];

            for (int i = 1; i <= leftDepth; i++)
            {
                if (candidate <= data.HighPrices[center - i])
                    return false;
            }

            return candidate > data.HighPrices[center + 1];
        }

        private bool IsEarlyPivotLow(Bars data, int center, int leftDepth, int availableThrough)
        {
            if (center - leftDepth < 0 || center + 1 > availableThrough)
                return false;

            double candidate = data.LowPrices[center];

            for (int i = 1; i <= leftDepth; i++)
            {
                if (candidate >= data.LowPrices[center - i])
                    return false;
            }

            return candidate < data.LowPrices[center + 1];
        }

        private bool HasEntryTrigger(int candidateIndex, int decisionIndex, double candidatePrice, bool isHigh)
        {
            double candidateHigh = Bars.HighPrices[candidateIndex];
            double candidateLow = Bars.LowPrices[candidateIndex];
            double candidateMidpoint = (candidateHigh + candidateLow) / 2.0;
            double decisionOpen = Bars.OpenPrices[decisionIndex];
            double decisionClose = Bars.ClosePrices[decisionIndex];

            double decisionRange = Bars.HighPrices[decisionIndex] - Bars.LowPrices[decisionIndex];
            if (decisionRange <= Symbol.TickSize)
                return false;

            double closeLocation = (decisionClose - Bars.LowPrices[decisionIndex]) / decisionRange;
            double bodyRatio = GetBodyRatio(decisionIndex);

            // Strong reversal candle: meaningful body and close near the reversal edge.
            bool m5ReversalClose = isHigh
                ? decisionClose < decisionOpen &&
                  decisionClose < candidateMidpoint &&
                  closeLocation <= 0.40 &&
                  bodyRatio >= 0.35
                : decisionClose > decisionOpen &&
                  decisionClose > candidateMidpoint &&
                  closeLocation >= 0.60 &&
                  bodyRatio >= 0.35;

            if (!m5ReversalClose)
                return false;

            if (UseM1Confirmation)
                return HasMicroStructureShift(candidateIndex, decisionIndex, candidatePrice, isHigh);

            // Disabling M1 makes the filter stricter rather than earlier: require
            // a full closed-M5 break beyond the candidate candle's complete range.
            return isHigh
                ? decisionClose < candidateLow
                : decisionClose > candidateHigh;
        }

        private bool HasMicroStructureShift(int candidateIndex, int decisionIndex, double candidatePrice, bool isHigh)
        {
            if (_m1Bars == null || _m1Bars.Count < 10)
                return false;

            DateTime candidateCloseTime = Bars.OpenTimes[candidateIndex].AddMinutes(5);
            DateTime decisionCloseTime = Bars.OpenTimes[decisionIndex].AddMinutes(5);
            int start = FindIndexAtOrAfter(_m1Bars, candidateCloseTime);
            int end = FindLastClosedBarIndex(_m1Bars, decisionCloseTime, 1);

            if (start < 3 || end < start + 2 || end >= _m1Bars.Count)
                return false;

            double atr = CalculateAtr(Bars, candidateIndex, AtrPeriod);
            double tolerance = Math.Max(Symbol.TickSize, atr * 0.02);

            // Reject the setup if the candidate extreme is breached again after
            // its M5 candle has closed.
            for (int i = start; i <= end; i++)
            {
                if (!isHigh && _m1Bars.LowPrices[i] < candidatePrice - tolerance)
                    return false;

                if (isHigh && _m1Bars.HighPrices[i] > candidatePrice + tolerance)
                    return false;
            }

            // M1 MSS/CHoCH: a closed M1 close must break the previous 3-bar micro range.
            for (int i = Math.Max(start, 3); i <= end; i++)
            {
                double priorHigh = double.MinValue;
                double priorLow = double.MaxValue;

                for (int j = i - 3; j < i; j++)
                {
                    priorHigh = Math.Max(priorHigh, _m1Bars.HighPrices[j]);
                    priorLow = Math.Min(priorLow, _m1Bars.LowPrices[j]);
                }

                double microRange = _m1Bars.HighPrices[i] - _m1Bars.LowPrices[i];
                if (microRange <= 0)
                    continue;

                double microBodyRatio = Math.Abs(_m1Bars.ClosePrices[i] - _m1Bars.OpenPrices[i]) / microRange;
                if (microBodyRatio < 0.45)
                    continue;

                double microAtr = CalculateAtr(_m1Bars, i, 14);
                double breakBuffer = Math.Max(Symbol.TickSize * 2, microAtr * 0.08);
                double closeLocation = (_m1Bars.ClosePrices[i] - _m1Bars.LowPrices[i]) / microRange;

                bool bullishDisplacement = _m1Bars.ClosePrices[i] > _m1Bars.OpenPrices[i] &&
                                           closeLocation >= 0.70 &&
                                           _m1Bars.ClosePrices[i] > priorHigh + breakBuffer;
                bool bearishDisplacement = _m1Bars.ClosePrices[i] < _m1Bars.OpenPrices[i] &&
                                           closeLocation <= 0.30 &&
                                           _m1Bars.ClosePrices[i] < priorLow - breakBuffer;

                if (!isHigh && bullishDisplacement)
                    return true;
                if (isHigh && bearishDisplacement)
                    return true;
            }

            return false;
        }

        private bool IsPivotHigh(Bars data, int center, int depth, int availableThrough)
        {
            if (center - depth < 0 || center + depth > availableThrough)
                return false;

            double candidate = data.HighPrices[center];

            for (int i = 1; i <= depth; i++)
            {
                if (candidate < data.HighPrices[center - i])
                    return false;

                // Prefer the last candle in an equal-high plateau.
                if (candidate <= data.HighPrices[center + i])
                    return false;
            }

            return true;
        }

        private bool IsPivotLow(Bars data, int center, int depth, int availableThrough)
        {
            if (center - depth < 0 || center + depth > availableThrough)
                return false;

            double candidate = data.LowPrices[center];

            for (int i = 1; i <= depth; i++)
            {
                if (candidate > data.LowPrices[center - i])
                    return false;

                // Prefer the last candle in an equal-low plateau.
                if (candidate >= data.LowPrices[center + i])
                    return false;
            }

            return true;
        }

        private double CalculateAtr(Bars data, int index, int period)
        {
            if (data == null || index < 1 || index >= data.Count)
                return 0;

            int start = Math.Max(1, index - period + 1);
            double sum = 0;
            int count = 0;

            for (int i = start; i <= index; i++)
            {
                double high = data.HighPrices[i];
                double low = data.LowPrices[i];
                double previousClose = data.ClosePrices[i - 1];
                double trueRange = Math.Max(high - low,
                    Math.Max(Math.Abs(high - previousClose), Math.Abs(low - previousClose)));

                if (trueRange < 0 || double.IsNaN(trueRange) || double.IsInfinity(trueRange))
                    continue;

                sum += trueRange;
                count++;
            }

            return count == 0 ? 0 : sum / count;
        }

        private double FindLocalOppositeExtreme(int center, bool isHigh, int depth, int availableThrough)
        {
            int start = Math.Max(0, center - depth);
            int end = Math.Min(availableThrough, center + depth);

            if (isHigh)
            {
                double lowest = double.MaxValue;
                for (int i = start; i <= end; i++)
                    lowest = Math.Min(lowest, Bars.LowPrices[i]);
                return lowest == double.MaxValue ? Bars.LowPrices[center] : lowest;
            }

            double highest = double.MinValue;
            for (int i = start; i <= end; i++)
                highest = Math.Max(highest, Bars.HighPrices[i]);
            return highest == double.MinValue ? Bars.HighPrices[center] : highest;
        }

        private double CalculateDisplacement(int pivotIndex, int decisionIndex, double pivotPrice, bool isHigh)
        {
            double furthestClose = isHigh ? double.MaxValue : double.MinValue;

            for (int i = pivotIndex + 1; i <= decisionIndex; i++)
            {
                double close = Bars.ClosePrices[i];
                furthestClose = isHigh
                    ? Math.Min(furthestClose, close)
                    : Math.Max(furthestClose, close);
            }

            if (furthestClose == double.MaxValue || furthestClose == double.MinValue)
                return 0;

            return isHigh
                ? Math.Max(0, pivotPrice - furthestClose)
                : Math.Max(0, furthestClose - pivotPrice);
        }

        private double GetRejectionWickRatio(int index, bool isHigh)
        {
            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            double open = Bars.OpenPrices[index];
            double close = Bars.ClosePrices[index];
            double range = high - low;

            if (range <= 0)
                return 0;

            double wick = isHigh
                ? high - Math.Max(open, close)
                : Math.Min(open, close) - low;

            return Clamp(wick / range, 0, 1);
        }

        private int WickScore(double ratio)
        {
            if (ratio >= 0.60) return 10;
            if (ratio >= 0.45) return 8;
            if (ratio >= 0.32) return 5;
            if (ratio >= 0.28) return 2;
            return 0;
        }

        private double GetBodyRatio(int index)
        {
            double range = Bars.HighPrices[index] - Bars.LowPrices[index];
            if (range <= 0) return 0;
            return Math.Abs(Bars.ClosePrices[index] - Bars.OpenPrices[index]) / range;
        }

        private int ConfirmationCandleScore(int index, bool isHigh)
        {
            double range = Bars.HighPrices[index] - Bars.LowPrices[index];
            if (range <= 0) return 0;

            double bodyRatio = GetBodyRatio(index);
            double closeLocation = (Bars.ClosePrices[index] - Bars.LowPrices[index]) / range;
            bool nearEdge = isHigh ? closeLocation <= 0.25 : closeLocation >= 0.75;
            bool acceptableEdge = isHigh ? closeLocation <= 0.35 : closeLocation >= 0.65;

            if (bodyRatio >= 0.65 && nearEdge) return 10;
            if (bodyRatio >= 0.50 && acceptableEdge) return 7;
            if (bodyRatio >= 0.35) return 3;
            return 0;
        }

        private bool HasStructureShift(int pivotIndex, int decisionIndex, Pivot previousOpposite, bool isHigh)
        {
            if (previousOpposite == null)
                return false;

            for (int i = pivotIndex + 1; i <= decisionIndex; i++)
            {
                if (isHigh && Bars.ClosePrices[i] < previousOpposite.Price)
                    return true;

                if (!isHigh && Bars.ClosePrices[i] > previousOpposite.Price)
                    return true;
            }

            return false;
        }

        private int CalculateHigherTimeframeBonus(double pivotPrice, bool isHigh, DateTime decisionCloseTime)
        {
            if (_m15Bars == null || _m15Bars.Count < 60)
                return 0;

            int htfIndex = FindLastClosedBarIndex(_m15Bars, decisionCloseTime, 15);
            if (htfIndex < 55)
                return 0;

            int bonus = 0;
            double fastAverage = AverageClose(_m15Bars, htfIndex, 20);
            double slowAverage = AverageClose(_m15Bars, htfIndex, 50);
            double htfClose = _m15Bars.ClosePrices[htfIndex];

            int trend = 0;
            if (fastAverage > slowAverage && htfClose > fastAverage) trend = 1;
            else if (fastAverage < slowAverage && htfClose < fastAverage) trend = -1;

            bool aligned = (isHigh && trend < 0) || (!isHigh && trend > 0);
            bool counterTrend = (isHigh && trend > 0) || (!isHigh && trend < 0);
            if (aligned) bonus += 8;

            bool nearSameSideSwing = false;
            int levelIndex = FindLatestConfirmedPivotIndex(_m15Bars, htfIndex, isHigh, 2, 96);
            double htfAtr = CalculateAtr(_m15Bars, htfIndex, 14);

            if (levelIndex >= 0 && htfAtr > 0)
            {
                double level = isHigh ? _m15Bars.HighPrices[levelIndex] : _m15Bars.LowPrices[levelIndex];
                nearSameSideSwing = Math.Abs(pivotPrice - level) <= htfAtr * 1.10;
                if (nearSameSideSwing) bonus += 10;
            }

            if (counterTrend && !nearSameSideSwing) bonus -= 18;
            return (int)Clamp(bonus, -18, 18);
        }

        private int FindLatestConfirmedPivotIndex(Bars data, int endIndex, bool isHigh, int depth, int lookback)
        {
            int earliest = Math.Max(depth, endIndex - lookback);
            int latest = endIndex - depth;

            for (int candidate = latest; candidate >= earliest; candidate--)
            {
                bool pivot = isHigh
                    ? IsPivotHigh(data, candidate, depth, endIndex)
                    : IsPivotLow(data, candidate, depth, endIndex);

                if (pivot)
                    return candidate;
            }

            return -1;
        }

        private int CalculateMicroConfirmationBonus(int pivotIndex, int decisionIndex, double pivotPrice, bool isHigh, double atr)
        {
            if (_m1Bars == null || _m1Bars.Count < 20)
                return 0;

            DateTime decisionCloseTime = Bars.OpenTimes[decisionIndex].AddMinutes(5);
            DateTime firstEligibleTime = Bars.OpenTimes[pivotIndex].AddMinutes(5);
            int end = FindLastClosedBarIndex(_m1Bars, decisionCloseTime, 1);
            int start = FindIndexAtOrAfter(_m1Bars, firstEligibleTime);

            if (start < 1 || end <= start || end >= _m1Bars.Count)
                return 0;

            int bonus = 0;
            int favorableCloseSteps = 0;
            int firstRecent = Math.Max(start + 1, end - 9);

            for (int i = firstRecent; i <= end; i++)
            {
                bool favorable = isHigh
                    ? _m1Bars.ClosePrices[i] < _m1Bars.ClosePrices[i - 1]
                    : _m1Bars.ClosePrices[i] > _m1Bars.ClosePrices[i - 1];

                if (favorable)
                    favorableCloseSteps++;
            }

            if (favorableCloseSteps >= 4)
                bonus += 4;
            else if (favorableCloseSteps >= 2)
                bonus += 2;

            double initialClose = _m1Bars.ClosePrices[start];
            double finalClose = _m1Bars.ClosePrices[end];
            bool netMove = isHigh ? finalClose < initialClose : finalClose > initialClose;

            if (netMove)
                bonus += 4;

            double moveFromPivot = isHigh ? pivotPrice - finalClose : finalClose - pivotPrice;
            if (moveFromPivot >= atr * 0.25)
                bonus += 2;

            return Math.Min(10, bonus);
        }

        private int FindLastClosedBarIndex(Bars data, DateTime decisionCloseTime, int durationMinutes)
        {
            if (data == null || data.Count == 0)
                return -1;

            int low = 0;
            int high = data.Count - 1;
            int result = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                DateTime closeTime = data.OpenTimes[mid].AddMinutes(durationMinutes);

                if (closeTime <= decisionCloseTime)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return result;
        }

        private int FindIndexAtOrAfter(Bars data, DateTime targetTime)
        {
            if (data == null || data.Count == 0)
                return -1;

            int low = 0;
            int high = data.Count - 1;
            int result = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (data.OpenTimes[mid] >= targetTime)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return result;
        }

        private double AverageClose(Bars data, int index, int period)
        {
            int start = Math.Max(0, index - period + 1);
            int count = index - start + 1;
            if (count <= 0)
                return data.ClosePrices[index];

            double sum = 0;
            for (int i = start; i <= index; i++)
                sum += data.ClosePrices[i];

            return sum / count;
        }

        private Pivot FindLastPivot(bool isHigh, DateTime beforeTime)
        {
            for (int i = _pivots.Count - 1; i >= 0; i--)
            {
                Pivot pivot = _pivots[i];
                if (pivot.Time < beforeTime && pivot.IsHigh == isHigh)
                    return pivot;
            }

            return null;
        }

        private string ClassifyStructure(bool isHigh, double price, Pivot previousSame, double atr)
        {
            if (previousSame == null)
                return isHigh ? "SH" : "SL";

            double tolerance = atr * 0.12;

            if (isHigh)
            {
                if (price > previousSame.Price + tolerance)
                    return "HH";
                if (price < previousSame.Price - tolerance)
                    return "LH";
                return "EQH";
            }

            if (price > previousSame.Price + tolerance)
                return "HL";
            if (price < previousSame.Price - tolerance)
                return "LL";
            return "EQL";
        }

        private void DrawPivot(Pivot pivot, Pivot previousOpposite, int decisionIndex)
        {
            // Default display contains only entry arrows; no zigzag and no swing text labels.
            double markerOffset = Math.Max(Symbol.TickSize * 2, pivot.Atr * 0.08);
            double labelOffset = Math.Max(Symbol.TickSize * 4, pivot.Atr * 0.22);
            string suffix = pivot.Time.Ticks.ToString(CultureInfo.InvariantCulture);

            if (ShowSwingMarkers)
            {
                double pivotMarkerPrice = pivot.Price + (pivot.IsHigh ? markerOffset : -markerOffset);
                Color swingColor = pivot.IsHigh ? Color.OrangeRed : Color.MediumSeaGreen;
                ChartIconType swingIcon = pivot.IsHigh ? ChartIconType.DownTriangle : ChartIconType.UpTriangle;

                pivot.IconName = "SwingM5_P_" + suffix;
                Chart.DrawIcon(pivot.IconName, swingIcon, pivot.Time, pivotMarkerPrice, swingColor);
                pivot.ObjectNames.Add(pivot.IconName);
            }

            // The arrow is time-stamped at the close of the confirming M5 candle,
            // i.e. when the signal has become available, not at the previous swing extreme.
            DateTime signalTime = Bars.OpenTimes[decisionIndex].AddMinutes(5);
            string side = pivot.IsHigh ? "SELL" : "BUY";
            Color signalColor = pivot.IsHigh ? Color.Red : Color.LimeGreen;
            double signalPrice = pivot.IsHigh
                ? Bars.ClosePrices[decisionIndex] + markerOffset
                : Bars.ClosePrices[decisionIndex] - markerOffset;
            double signalLabelPrice = pivot.IsHigh
                ? signalPrice + labelOffset
                : signalPrice - labelOffset;

            pivot.EntryIconName = "SwingM5_E_" + signalTime.Ticks.ToString(CultureInfo.InvariantCulture);
            Chart.DrawIcon(pivot.EntryIconName,
                pivot.IsHigh ? ChartIconType.DownArrow : ChartIconType.UpArrow,
                signalTime, signalPrice, signalColor);
            pivot.ObjectNames.Add(pivot.EntryIconName);

            if (ShowScoreLabels)
            {
                pivot.EntryTextName = "SwingM5_ET_" + signalTime.Ticks.ToString(CultureInfo.InvariantCulture);
                Chart.DrawText(pivot.EntryTextName,
                    side + " " + pivot.Score.ToString(CultureInfo.InvariantCulture),
                    signalTime, signalLabelPrice, signalColor);
                pivot.ObjectNames.Add(pivot.EntryTextName);
            }

            if (IsLastBar)
            {
                Print("SwingM5 {0} confirmed | {1} | score {2} | pivot {3} | confirmation close {4}",
                    side, SymbolName, pivot.Score, pivot.Price, signalTime);

                if (SoundAlertOnEntryConfirmation)
                    Notifications.PlaySound(SoundType.PositiveNotification);
            }

            _visiblePivots.Add(pivot);
            while (_visiblePivots.Count > MaximumDisplayedSwings)
            {
                Pivot oldest = _visiblePivots[0];
                for (int i = 0; i < oldest.ObjectNames.Count; i++)
                    Chart.RemoveObject(oldest.ObjectNames[i]);

                _visiblePivots.RemoveAt(0);
            }
        }

        private double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }

        private sealed class Pivot
        {
            public int Index { get; set; }
            public DateTime Time { get; set; }
            public double Price { get; set; }
            public bool IsHigh { get; set; }
            public int Score { get; set; }
            public string Structure { get; set; }
            public double Atr { get; set; }
            public string IconName { get; set; }
            public string EntryIconName { get; set; }
            public string EntryTextName { get; set; }
            public List<string> ObjectNames { get; } = new List<string>();
        }
    }
}