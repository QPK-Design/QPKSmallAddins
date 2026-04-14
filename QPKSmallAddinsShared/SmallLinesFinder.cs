using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace QPKSmallAddins
{
    [Transaction(TransactionMode.Manual)]
    public class SmallLinesFinder : IExternalCommand
    {
        // 1. Keep a static reference to the WINDOW, so we don't open multiples.
        private static SmallLinesWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument uiDoc = app.ActiveUIDocument;
            Document doc = uiDoc?.Document;

            if (uiDoc == null)
            {
                message = "No active Revit document is open.";
                TaskDialog.Show("Small Lines Finder", message);
                return Result.Failed;
            }

            // 2. KEYNOTE TEMPLATE: If the window is already open, bring it to the front and stop.
            if (_window != null)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            List<ElementId> selectedIds = new List<ElementId>();

            // 3. Check if the user pre-selected elements before running the command
            var currentSelection = uiDoc.Selection.GetElementIds();
            foreach (var id in currentSelection)
            {
                if (doc.GetElement(id) is CurveElement)
                {
                    selectedIds.Add(id);
                }
            }

            // 4. If no valid lines were pre-selected, prompt them to pick
            if (selectedIds.Count == 0)
            {
                try
                {
                    IList<Reference> refs = uiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LineSelectionFilter(),
                        "Select lines to analyse (Detail Lines & Model Lines only)");

                    selectedIds = refs.Select(r => r.ElementId).ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User pressed ESC
                    return Result.Cancelled;
                }
            }

            if (selectedIds.Count == 0) return Result.Cancelled;

            // 5. KEYNOTE TEMPLATE: Create window if none exists, and reset the variable when closed.
            if (_window == null)
            {
                _window = new SmallLinesWindow(app, selectedIds);
                _window.Closed += (s, e) => _window = null;

                // We keep this as ShowDialog() (Modal) instead of Show() (Modeless) 
                // because your window has a "Delete" button that needs safe API access!
                _window.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}