using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace QPKSmallAddins
{
    [Transaction(TransactionMode.Manual)]
    public class WorksetPreloader : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (uiDoc == null)
            {
                message = "No active Revit document is open.";
                TaskDialog.Show("QPK Worksets", message);
                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            List<string> worksetNames = new List<string>
            {
                "Architecture",
                "Linked Structure",
                "Linked MEP",
                "Site",
                "Rendering",
                "Hidden Elements",
                "Shared Levels and Grids"
            };

            try
            {
                // If not already workshared, enable worksharing first.
                if (!doc.IsWorkshared)
                {
                    doc.EnableWorksharing("Shared Levels and Grids", "Workset1");
                }

                using (Transaction trans = new Transaction(doc, "Create Predefined Worksets"))
                {
                    trans.Start();

                    foreach (string worksetName in worksetNames)
                    {
                        if (!WorksetExists(doc, worksetName))
                        {
                            Workset.Create(doc, worksetName);
                        }
                    }

                    trans.Commit();
                }

                TaskDialog.Show("QPK Worksets", "Predefined worksets created successfully.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("QPK Worksets Error", ex.Message);
                return Result.Failed;
            }
        }

        private static bool WorksetExists(Document doc, string worksetName)
        {
            FilteredWorksetCollector collector = new FilteredWorksetCollector(doc);
            foreach (Workset ws in collector.OfKind(WorksetKind.UserWorkset))
            {
                if (ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}