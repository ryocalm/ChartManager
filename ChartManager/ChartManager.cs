using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Ext;
using cAlgo.API.Ext.Chart;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ChartManager : Indicator
    {
        [Parameter("Observation mode", DefaultValue = false)]
        public bool ObservationMode { get; set; }

        private HorizontalLineMarker[]? LineMarkers { get; set; }

        private ChartObjectsDefaultSetting? _defaultSetting;

        /// <summary>
        /// ChartObject 追加時に実行する。
        /// </summary>
        /// <param name="args"></param>
        private void OnObjectsAdded(ChartObjectsAddedEventArgs args)
        {
            if (_defaultSetting == null)
            {
                return;
            }

            foreach (var obj in args.ChartObjects)
            {
                _defaultSetting.OnAdded(obj);
            }
        }

        /// <summary>
        /// ChartObject 更新時に実行する。
        /// </summary>
        /// <param name="args"></param>
        private void OnObjectsUpdated(ChartObjectsUpdatedEventArgs args)
        {
            if (_defaultSetting == null)
            {
                return;
            }

            foreach (var obj in args.ChartObjects)
            {
                _defaultSetting.OnUpdated(obj);
            }
        }

        private void ConvertEllipseToLineMarker()
        {
            var ellipses = Chart.FindAllObjects<ChartEllipse>();

            if (!ellipses.Any())
            {
                LineMarkers = null;
                return;
            }

            LineMarkers = ellipses
                .Select(ellipse => new HorizontalLineMarker(ellipse, Symbol.Digits))
                .ToArray();
        }

        /// <summary>
        /// LineMarker の位置に VerticalLine を引く
        /// </summary>
        /// <param name="horizontalLine"></param>
        private void DrawMarkerLine(ChartHorizontalLine horizontalLine)
        {
            if (LineMarkers is null)
            {
                return;
            }

            var y = Math.Round(horizontalLine.Y, Symbol.Digits);
            Print("name: {0}, y: {1}", horizontalLine.Name, y.ToString(CultureInfo.CurrentCulture));

            // LineMarkers の中に MidY == y となる Marker があるか
            const double tolerance = 0.01;
            var markers = LineMarkers
                .Where(marker => Math.Abs(marker.MidY - y) < tolerance)
                .ToArray();

            if (!markers.Any())
            {
                Print("There is no marker.");
                return;
            }

            foreach (var marker in markers)
            {
                if (marker.Ellipse.IsHidden)
                {
                    marker.Ellipse.IsHidden = false;
                }

                var verticalLine = Chart.DrawVerticalLine(
                    name: marker.Name,
                    time: marker.MidTime,
                    color: Color.HotPink,
                    thickness: 2,
                    lineStyle: LineStyle.DotsVeryRare);
                verticalLine.IsInteractive = true;
                verticalLine.IsHidden = false;
            }

            horizontalLine.LineStyle = LineStyle.LinesDots;
            Thread.Sleep(300);
        }

        /// <summary>
        /// VerticalLine を非表示にする
        /// </summary>
        private void HideMarkerLines()
        {
            var markerLines = Chart.FindAllObjects<ChartVerticalLine>()
                .Where(verticalLine => verticalLine.Name.StartsWith("marker"))
                .ToArray();

            if (!markerLines.Any())
            {
                return;
            }

            Print("=== There are some markerLines. ===");
            foreach (var line in markerLines)
            {
                line.IsHidden = true;
            }
        }

        /// <summary>
        /// ChartObject 選択時に実行する。
        /// 個々の object に対する処理。
        /// </summary>
        /// <param name="obj"></param>
        private void OnAddedToSelection(ChartObject obj)
        {
            switch (obj.ObjectType)
            {
                case ChartObjectType.HorizontalLine:
                    if (obj is not ChartHorizontalLine horizontalLine)
                    {
                        return;
                    }

                    ConvertEllipseToLineMarker();
                    DrawMarkerLine(horizontalLine);
                    break;
            }
        }

        /// <summary>
        /// ChartObject 選択解除時に実行する。
        /// 個々の object に対する処理。
        /// </summary>
        /// <param name="obj"></param>
        private void OnRemovedFromSelection(ChartObject obj)
        {
            switch (obj.ObjectType)
            {
                case ChartObjectType.HorizontalLine:
                    HideMarkerLines();

                    if (obj is not ChartHorizontalLine horizontalLine)
                    {
                        return;
                    }

                    if (horizontalLine.LineStyle == LineStyle.LinesDots)
                    {
                        horizontalLine.LineStyle = LineStyle.DotsRare;
                    }

                    break;
            }
        }

        /// <summary>
        /// ChartObject 選択時・選択解除時に実行する。
        /// </summary>
        /// <param name="args"></param>
        private void OnObjectsSelectionChanged(ChartObjectsSelectionChangedEventArgs args)
        {
            foreach (var obj in args.ObjectsAddedToSelection)
            {
                OnAddedToSelection(obj);
            }

            foreach (var obj in args.ObjectsRemovedFromSelection)
            {
                OnRemovedFromSelection(obj);
            }
        }

        protected override void Initialize()
        {
            _defaultSetting = new ChartObjectsDefaultSetting(Chart, ObservationMode);

            // manage chart color settings
            Chart.SetColor();

            // manage chart display settings
            Chart.DisplaySettings.Positions = true;
            Chart.DisplaySettings.Orders = true;
            Chart.DisplaySettings.BidPriceLine = true;
            Chart.DisplaySettings.AskPriceLine = true;
            Chart.DisplaySettings.Grid = (Chart.TimeFrame <= TimeFrame.Minute15);
            Chart.DisplaySettings.PeriodSeparators = true;
            Chart.DisplaySettings.TickVolume = false;
            Chart.DisplaySettings.DealMap = false;
            Chart.DisplaySettings.ChartScale = true;
            Chart.DisplaySettings.PriceAxisOverlayButtons = true;
            Chart.DisplaySettings.PriceAlerts = true;
            Chart.DisplaySettings.QuickTradeButtons = true;
            Chart.DisplaySettings.MarketSentiment = false;

            ConvertEllipseToLineMarker();

            // manage ChartObjects
            Chart.ObjectsAdded += OnObjectsAdded;
            Chart.ObjectsUpdated += OnObjectsUpdated;
            Chart.ObjectsSelectionChanged += OnObjectsSelectionChanged;

            Chart.HideChartObjects();
            Chart.ShowChartObjects();

            // indicate TimeFrame
            var timeFrameButton = new Button
            {
                Text = TimeFrame.ToShorthand(),
                Height = 48,
                Width = 100,
                CornerRadius = new CornerRadius(5),
                FontSize = 36,
                BackgroundColor = Color.Transparent,
                ForegroundColor = Color.LightGray,
                Margin = 2,
                Padding = 0,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            Chart.AddControl(timeFrameButton);

            Chart.DrawHorizontalLine("line name", Ask + Symbol.PipSize * 10, Color.Aqua).IsInteractive = true;
            Chart.DrawHorizontalLine("line name2", Ask - Symbol.PipSize * 10, Color.Aqua).IsInteractive = true;

            // var lines = Chart.FindAllObjects<ChartHorizontalLine>();
            // Print(lines.Length);
            // foreach (var line in lines)
            // {
            //     Print(line.Comment);
            // }
        }

        public override void Calculate(int index)
        {
            // indicate spread
            var spread = Math.Round(
                value: (Symbol.Spread / Symbol.PipSize),
                digits: 5,
                mode: MidpointRounding.AwayFromZero);

            var spreadString = spread.ToString("0.0");
            Chart.DrawStaticText(
                name: "spread",
                text: spreadString,
                verticalAlignment: VerticalAlignment.Center,
                horizontalAlignment: HorizontalAlignment.Right,
                color: Color.SlateGray);
        }
    }
}