using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace QPKSmallAddins
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Data Model & Filters
    // ─────────────────────────────────────────────────────────────────────────
    public class LineData
    {
        public long ElementId { get; set; }
        public ElementId RevitId { get; set; } = null!;
        public double LengthFeet { get; set; }
        public string LengthDisplay { get; set; } = string.Empty;
    }

    public class LineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is CurveElement;
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Code-behind
    // ─────────────────────────────────────────────────────────────────────────
    public partial class SmallLinesWindow : Window
    {
        private readonly UIApplication _app;
        private UIDocument UIDoc => _app.ActiveUIDocument;
        private Document Doc => UIDoc?.Document;

        private List<ElementId> _selectedIds;
        private List<LineData> _lineData = new List<LineData>();

        // ── Constructor ───────────────────────────────────────────────────────
        public SmallLinesWindow(UIApplication app, List<ElementId> selectedIds)
        {
            _app = app;
            _selectedIds = selectedIds;
            InitializeComponent();

            new WindowInteropHelper(this).Owner = app.MainWindowHandle;

            SetStatus($"{_selectedIds.Count} line(s) selected — press Measure Lines to analyse.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  "Measure Lines" – parse threshold, collect lengths, populate grid
        // ─────────────────────────────────────────────────────────────────────
        private void BtnMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (UIDoc == null) return;

            double thresholdFeet = 0;
            string rawInput = TxtMinLength.Text.Trim();

            if (!string.IsNullOrWhiteSpace(rawInput))
            {
                if (!TryParseRevitLength(Doc, rawInput, out thresholdFeet))
                {
                    MessageBox.Show(
                        "Could not parse the Min Length value.\n\n" +
                        "Enter a value in the same format Revit uses for this project, e.g.:\n" +
                        "  1/64\"   3/16\"   6\"   1'-3\"   4'-7 1/16\"",
                        "Parse Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var allData = new List<LineData>();
            foreach (ElementId id in _selectedIds)
            {
                Element el = Doc.GetElement(id);
                if (el == null) continue;

                double len = GetCurveLengthFeet(el);

                allData.Add(new LineData
                {
                    ElementId = id.Value, // Using .Value for Revit 2024+
                    RevitId = id,
                    LengthFeet = len,
                    LengthDisplay = FormatRevitLength(Doc, len)
                });
            }

            _lineData = (thresholdFeet > 0
                ? allData.Where(l => l.LengthFeet <= thresholdFeet + 1e-10)
                : allData)
                .OrderBy(l => l.LengthFeet)
                .ToList();

            ResultsDataGrid.ItemsSource = null;
            ResultsDataGrid.ItemsSource = _lineData;

            BtnDeleteAll.IsEnabled = _lineData.Count > 0;
            SetStatus(_lineData.Count == 0
                ? "No lines match the filter."
                : $"{_lineData.Count} line(s) found (sorted smallest first).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Row selection → highlight element in Revit view
        // ─────────────────────────────────────────────────────────────────────
        private void ResultsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is LineData row && UIDoc != null)
            {
                try
                {
                    UIDoc.Selection.SetElementIds(new List<ElementId> { row.RevitId });
                    UIDoc.ShowElements(row.RevitId);
                }
                catch { /* Element may have been deleted externally */ }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  "Delete All"
        // ─────────────────────────────────────────────────────────────────────
        private void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (_lineData.Count == 0 || Doc == null) return;

            var confirm = MessageBox.Show(
                $"Permanently delete {_lineData.Count} line(s) from the Revit model?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using (Transaction tx = new Transaction(Doc, "QPK – Delete Small Lines"))
                {
                    tx.Start();
                    foreach (LineData ld in _lineData)
                        Doc.Delete(ld.RevitId);
                    tx.Commit();
                }

                _lineData.Clear();
                _selectedIds.Clear();
                ResultsDataGrid.ItemsSource = null;
                BtnDeleteAll.IsEnabled = false;
                SetStatus("All listed lines deleted.");

                MessageBox.Show("Success", "Small Lines Finder",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during deletion:\n{ex.Message}",
                                "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Revit API helpers (Cleaned up for Revit 2024)
        // ─────────────────────────────────────────────────────────────────────
        private void SetStatus(string text) => LblStatus.Content = text;

        private static double GetCurveLengthFeet(Element el)
        {
            if (el is CurveElement ce && ce.GeometryCurve != null)
                return ce.GeometryCurve.Length;

            Parameter p = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            return p?.AsDouble() ?? 0.0;
        }

        private static bool TryParseRevitLength(Document doc, string input, out double internalFeet)
        {
            internalFeet = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            return UnitFormatUtils.TryParse(
                doc.GetUnits(),
                SpecTypeId.Length,
                input,
                out internalFeet);
        }

        private static string FormatRevitLength(Document doc, double internalFeet)
        {
            try
            {
                return UnitFormatUtils.Format(
                    doc.GetUnits(),
                    SpecTypeId.Length,
                    internalFeet,
                    false);
            }
            catch
            {
                return $"{internalFeet:F4} ft";
            }
        }
    }
}