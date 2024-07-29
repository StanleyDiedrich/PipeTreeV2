using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeTreeV2
{
    public class ContextViewModel : INotifyPropertyChanged
    {
        private IList<string> systemNames;
        public IList<string> SystemNames
        {
            get { return systemNames; }
            set
            {
                systemNames = value;
                OnPropertyChanged("SystemNames");
            }
        }

        private Autodesk.Revit.DB.Document document;
        public Autodesk.Revit.DB.Document Document
        {
            get { return document; }
            set
            {
                document = value;
                OnPropertyChanged("Document");
            }
        }
        private string selectedSystemName;
        public string SelectedSystemName
        {
            get { return selectedSystemName; }
            set
            {
                selectedSystemName = value;
                OnPropertyChanged("SelectedSystemName");
            }
        }

        private List<ElementId> startTracks;
        public List<ElementId> StartTracks
        {
            get { return startTracks; }
            set
            {
                startTracks = value;
                OnPropertyChanged("StartTracks");
            }
        }
        private List<ElementId> connectors;
        public List<ElementId> Connectors
        {
            get { return connectors; }
            set
            {
                connectors = value;
                OnPropertyChanged("Connectors");
            }
        }
        private List<ElementId> modelElements;
        public List<ElementId> ModelElements
        {
            get { return modelElements; }
            set
            {
                modelElements = value;
                OnPropertyChanged("ModelElements");
            }
        }
        private List<Element> mepSystems;
        public List<Element> MEPSystems
        {
            get { return mepSystems; }
            set
            {
                mepSystems = value;
                OnPropertyChanged("MEPSystems");
            }
        }



        public ICommand StartCommand { get; private set; }

        public List<ElementId> GetPipes(Autodesk.Revit.DB.Document Document, string selectedSystemName)
        {

            List<ElementId> startPipes = new List<ElementId>();

            List<Element> syspipes = new List<Element>();
            var pipes = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements();
            foreach (Element pipe in pipes)
            {
                var newpipe = pipe as Pipe;
                var fI = newpipe as MEPCurve;
                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    syspipes.Add(pipe);
                }

            }
            var pipesBySystem = syspipes.GroupBy(p => ((Pipe)p).get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString()).ToList();

            foreach (var groups in pipesBySystem)
            {
                Element maxpipe = null;
                double maxvolume = -10000000;
                foreach (Element pipe in groups)
                {


                    if (pipe.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble() >= maxvolume)
                    {
                        maxpipe = pipe;
                        maxvolume = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble();
                    }
                }
                startPipes.Add(maxpipe.Id);


            }

            /* var pipesWithMaxFlow = pipesBySystem
             .SelectMany(x => x)
             .GroupBy(p => p.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsString())
             .SelectMany(group => group.Where(p => p.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble() == group.Max(g => g.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble())));*/

            //List<Element> pipesListWithMaxFlow = pipesWithMaxFlow.ToList();

            return startPipes;

        }
        public List<Element> GetMepSystems(Autodesk.Revit.DB.Document document, string selectedSystemName)
        {
            List<Element> mepsystems = new List<Element>();
            FilteredElementCollector systems = new FilteredElementCollector(document).OfClass(typeof(MEPSystem));
            mepsystems = systems.Select(x => x).Where(y => y.Name.Contains(selectedSystemName)).ToList();
            return mepsystems;
        }
        public List<ElementId> GetConnectors(Autodesk.Revit.DB.Document Document, string selectedSystemName)
        {
            List<ElementId> startConnectors = new List<ElementId>();


            var pipes = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElements();
            foreach (Element pipe in pipes)
            {
                var fI = pipe as FamilyInstance;

                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    startConnectors.Add(fI.Id);
                }

            }
            return startConnectors;
        }
        public List<ElementId> GetModelElements(Autodesk.Revit.DB.Document Document, string selectedSystemName)
        {
            List<ElementId> modelElements = new List<ElementId>();


            var mechEquip = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElements();
            var pipes = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements();
            var fittings = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType().ToElements();
            var armatura = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeAccessory).WhereElementIsNotElementType().ToElements();

            foreach (var el in mechEquip)
            {
                var fI = el as FamilyInstance;

                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    modelElements.Add(fI.Id);
                }
            }
            /*foreach (var el in armatura)
            {
                var fI = el as FamilyInstance;

                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    modelElements.Add(fI.Id);
                }
            }*/
            /*foreach (var el in fittings)
            {
                var fI = el as FamilyInstance;

                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    modelElements.Add(fI.Id);
                }
            }*/

            foreach (Element pipe in pipes)
            {
                var newpipe = pipe as Pipe;
                var fI = newpipe as MEPCurve;
                if (fI.LookupParameter("Имя системы").AsString().Contains(selectedSystemName))
                {
                    modelElements.Add(newpipe.Id);
                }

            }

            return modelElements;
        }






        private void GetTracks(object parameter)
        {

            // StartTracks = GetPipes(Document, SelectedSystemName);
            //Connectors = GetConnectors(Document, SelectedSystemName);
            // ModelElements = GetModelElements(Document, SelectedSystemName);
            MEPSystems = GetMepSystems(Document, SelectedSystemName);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ContextViewModel(IList<string> systems, Autodesk.Revit.DB.Document doc)
        {
            SystemNames = systems;
            Document = doc;
            StartCommand = new RelayCommand(GetTracks);
            StartTracks = new List<ElementId>();
            Connectors = new List<ElementId>();
            ModelElements = new List<ElementId>();
            MEPSystems = new List<Element>();

        }

        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
