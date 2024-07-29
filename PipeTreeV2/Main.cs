using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Configuration;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using Application = Autodesk.Revit.Creation.Application;

namespace PipeTreeV2
{

   

    


    

        
        

        

        
       
    public class InfoContext
    {
        public ContextViewModel ContextViewModel { get; set; }

        public InfoContext(IList<string> systemnames, Autodesk.Revit.DB.Document doc)
        {
            ContextViewModel = new ContextViewModel(systemnames, doc);
        }
    }
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]






    public class Main : IExternalCommand
    {


        static AddInId AddInId = new AddInId(new Guid("CDFCB89B-70AD-452A-91A7-EB47D70781BF"));







        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.DB.Document doc = uidoc.Document;





            ///
            List<string> systemnames = new List<string>();
            IList<Element> pipes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements();
            foreach (Element pipe in pipes)
            {
                var newpipe = pipe as Pipe;
                var fI = newpipe as MEPCurve;
                foreach (Parameter parameter in newpipe.Parameters)
                {
                    if (parameter.Definition.Name.Equals("Сокращение для системы"))
                    {
                        string system = parameter.AsString();
                        if (system != null)
                        {
                            if (!systemnames.Contains(system))
                            {
                                systemnames.Add(system);
                            }
                        }
                    }
                }
            }


            Window window = new Window();
            InfoContext dataContext = new InfoContext(systemnames, doc);
            window.DataContext = dataContext;
            window.ShowDialog();
            ///

            
               
               

            


           
            

            return Result.Succeeded;
        }
    }
}
