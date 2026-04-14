using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace QPKSmallAddins
{
    [Transaction(TransactionMode.Manual)]
    public class ShowSmallLinesWindowCommand : IExternalCommand
    {
        private static SmallLinesWindow _window;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    TaskDialog.Show("Small Lines Finder", "No active Revit document was found.");
                    return Result.Cancelled;
                }

                IList<Reference> pickedRefs;
                try
                {
                    pickedRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LineSelectionFilter(),
                        "Select detail lines / model lines to review");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                List<ElementId> selectedIds = pickedRefs
                    .Select(r => r.ElementId)
                    .Distinct()
                    .ToList();

                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Small Lines Finder", "No valid lines were selected.");
                    return Result.Cancelled;
                }

                if (_window == null)
                {
                    _window = new SmallLinesWindow(uiapp, selectedIds);
                    _window.Closed += (s, e) => _window = null;
                    _window.Show();
                }
                else
                {
                    if (!_window.IsVisible)
                    {
                        _window = new SmallLinesWindow(uiapp, selectedIds);
                        _window.Closed += (s, e) => _window = null;
                        _window.Show();
                    }
                    else
                    {
                        _window.Close();
                        _window = new SmallLinesWindow(uiapp, selectedIds);
                        _window.Closed += (s, e) => _window = null;
                        _window.Show();
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}