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
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Application = Autodesk.Revit.Creation.Application;

namespace PipeTreeV2
{



    public class Node
    {
        public ElementId ElementId;
        public ElementId Neighbourgh;

        public Node(Autodesk.Revit.DB.Document doc, ElementId elementId, PipeSystemType pipeSystemType)
        {
            ElementId = elementId;
            Neighbourgh = FindNachbar(doc, elementId, pipeSystemType);
        }

        public ElementId FindNachbar(Autodesk.Revit.DB.Document doc, ElementId elementId, PipeSystemType pipeSystemType)
        {
            Element element = doc.GetElement(elementId);
            MEPModel mepModel = null;
            ConnectorSet connectorSet = null;
            ElementId foundedelementId = null;

            try
            {
                if (element is FamilyInstance fi)
                {
                    mepModel = fi.MEPModel;
                    connectorSet = mepModel.ConnectorManager.Connectors;
                }

                if (element is Pipe pipe)
                {
                    connectorSet = pipe.ConnectorManager.Connectors;
                }
                else if (element is FlexDuct flexDuct)
                {
                    connectorSet = flexDuct.ConnectorManager.Connectors;
                }

                foreach (Connector connector in connectorSet)
                {
                    double connectorFlow = connector.Flow;
                    if (connector.PipeSystemType == pipeSystemType)
                    {
                        ConnectorSet nextConnectors = connector.AllRefs;

                        foreach (Connector nextConnector in nextConnectors)
                        {
                            // Игнорируем если это PipingSystem
                            if (doc.GetElement(nextConnector.Owner.Id) is PipingSystem)
                            {
                                continue;
                            }
                            else if (nextConnector.Owner.Id == elementId)
                            {
                                continue; // Игнорируем те же элементы
                            }
                            else if (nextConnectors.Size < 1)
                            {
                                continue;
                            }
                            else
                            {
                                if (nextConnector.Domain == Domain.DomainHvac || nextConnector.Domain == Domain.DomainPiping)
                                {
                                    double nextConnectorFlow = nextConnector.Flow;

                                    // Обработка в зависимости от типа системы
                                    if (pipeSystemType == PipeSystemType.SupplyHydronic)
                                    {
                                        if (nextConnector.Direction == FlowDirectionType.Out)
                                        {
                                            // Сравниваем потоки
                                            if (nextConnectorFlow >= connectorFlow)
                                            {
                                                foundedelementId = nextConnector.Owner.Id;
                                                return foundedelementId;
                                            }
                                        }
                                    }
                                    else if (pipeSystemType == PipeSystemType.ReturnHydronic)
                                    {
                                        if (nextConnector.Direction == FlowDirectionType.In)
                                        {
                                            foundedelementId = nextConnector.Owner.Id;
                                            return foundedelementId;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Тут можно было бы добавить обработку исключений
            }

            return null; // Возвращаем null, если ничего не найдено
        }
    }




















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

        static IList<Element> GetSystems(Autodesk.Revit.DB.Document document)
        {
            IList<Element> systems = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipingSystem).WhereElementIsNotElementType().ToElements();
            return systems;
        }





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


            UserControl1 window = new UserControl1();
            InfoContext dataContext = new InfoContext(systemnames, doc);
            window.DataContext = dataContext;
            window.ShowDialog();
            ///

            var systems = GetSystems(doc);
            List<Element> selectedsystems = new List<Element>();
            var flowdirectiontype = (((MEPSystem)systems.First() as PipingSystem).SystemType);
            //Тут фильтруем системы по наличию в имени сокращения
            foreach (var sys in systems)
            {
                if (sys.Name.Contains(dataContext.ContextViewModel.SelectedSystemName))
                {
                    selectedsystems.Add(sys);
                }
            }

            string csvcontent = "";

            var connectors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElementIds();

            List<List<Node>> nodes = new List<List<Node>>();

            // Итерируемся по каждому элементу из коллекции connectors
            foreach (ElementId connector in connectors)
            {
                // Создаем новый список для найденных узлов для текущего коннектора
                List<Node> foundedNodes = new List<Node>();

                // Создаем новый узел
                Node newNode = new Node(doc, connector, flowdirectiontype);
                foundedNodes.Add(newNode); // Добавляем начальный узел в список найденных узлов

                int counter = 0;

                // Цикл для проверки соседних узлов, ограничиваем 1000 итерациями 
                do
                {
                    // Получаем следующий узел (соседний) 
                    ElementId newConnector = newNode.Neighbourgh;
                    bool existsInAnyList = false;

                    foreach (var listOfNodes in nodes)
                    {
                        // Используем LINQ для быстрой проверки наличия currentId 
                        if (listOfNodes.Any(node => node.ElementId.Equals(newConnector)))
                        {
                            existsInAnyList = true;
                            break; // Если найдено хотя бы одно совпадение, выходим из цикла 
                        }
                    }

                    // Создаем новый узел только если соседний узел не найден в существующих
                    if (!existsInAnyList && newConnector != null)
                    {
                        // Создаем новый узел на основе соседнего элемента 
                        newNode = new Node(doc, newConnector, flowdirectiontype);
                        foundedNodes.Add(newNode); // Добавляем соседний узел 
                    }
                    else
                    {
                        break; // Выходим из цикла, если соседний узел уже существует или равен null
                    }

                    counter++;
                }
                
                while (newNode.Neighbourgh != null && counter < 100); // Выход из цикла если достигнуто 1000 итераций 
                nodes.Add(foundedNodes);
                


                

                
            }
            int branchcounter = 0;
            foreach (var startconnectors in nodes)
            {
                
                foreach (var startconnector in startconnectors)
                {

                    string a = $"{branchcounter};{startconnector.ElementId};" + "\n";
                    csvcontent += a;



                }
                branchcounter++;

            }
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();


            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.Title = "Save CSV File";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        writer.Write(csvcontent);
                    }

                    Console.WriteLine("CSV file saved successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving CSV file: " + ex.Message);
                }

            }
            return Result.Succeeded;
        }
    }
}


