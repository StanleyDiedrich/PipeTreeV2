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
    
    public class ModelNode
    {
        public ElementId ModelElementId { get; set; }
        public int Counter { get; set; }
        public List<ElementId> Neighbour { get; set; } = new List<ElementId>();
        public int ElementCounter { get; set; }
        public Dictionary<ElementId, List<ElementId>> Connections { get; set; } = new Dictionary<ElementId, List<ElementId>>();
        public string SystemName { get; set; }
        public bool IsVisited { get; set; }
        public ModelNode(Autodesk.Revit.DB.Document document, ElementId elementId)
        {
            ModelElementId = elementId;

            Element element = document.GetElement(ModelElementId);
            SystemName = element.LookupParameter("Имя системы").AsString();
            ConnectorSet connectorSet = GetConnectorSet(element); // Вынесение логики получения ConnectorSet в отдельный метод  
            if (connectorSet != null)
            {
                PopulateVertices(connectorSet); // Вынесение логики заполнения Vertices в отдельный метод 
                PopulateConnections(connectorSet);
            }
        }
        private ConnectorSet GetConnectorSet(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                return familyInstance.MEPModel?.ConnectorManager.Connectors;
            }
            else if (element is Pipe pipe)
            {
                return pipe.ConnectorManager.Connectors;
            }

            return null; // Не найден соответствующий ConnectorSet 
        }

        private void PopulateVertices(ConnectorSet connectorSet)
        {
            foreach (Connector connector in connectorSet)
            {
                ConnectorSet nextconnectors = connector.AllRefs;
                foreach (Connector nextconnector in nextconnectors)
                {
                    if (nextconnector.Domain != Domain.DomainUndefined && nextconnector.Owner != null)
                    {
                        if (nextconnector.Owner is PipingSystem || nextconnector.Owner.Id == connector.Owner.Id)
                        {
                            continue;
                        }
                        
                        else if (!Neighbour.Contains(nextconnector.Owner.Id))
                        {
                            if (nextconnector.Direction == FlowDirectionType.Out )
                            {
                                Neighbour.Add(nextconnector.Owner.Id);
                            }

                        }
                    }
                }
            }
        }
        private void PopulateConnections(ConnectorSet connectorSet)
        {
            foreach (Connector connector in connectorSet)
            {
                ConnectorSet nextconnectors = connector.AllRefs;
                foreach (Connector nextconnector in nextconnectors)
                {
                    if (nextconnector.Domain != Domain.DomainUndefined && nextconnector.Owner != null)
                    {
                        if (nextconnector.Direction == FlowDirectionType.Out)
                        {
                            ElementId ownerId = nextconnector.Owner.Id;
                            if (!Connections.ContainsKey(ownerId))
                            {
                                Connections[ownerId] = new List<ElementId>();
                            }
                            Connections[ownerId].Add(connector.Owner.Id); // Добавляем связь в словарь
                        }
                        
                    }
                }
            }
        }
        public void RecursiveTraversal(Autodesk.Revit.DB.Document document, List<ElementId> visitedIds)
        {
            // Добавляем текущий элемент в список посещенных 
            visitedIds.Add(ModelElementId);

            // Получаем имя системы для текущего элемента
            string systemname = document.GetElement(ModelElementId).LookupParameter("Имя системы").AsString();

            // Обходим соседние элементы 
            foreach (ElementId neighbourId in Neighbour)
            {
                // Проверяем, был ли сосед уже посещен
                if (!visitedIds.Contains(neighbourId))
                {
                    // Получаем модель узла по соседнему id 
                    ModelNode neighbourNode = GetModelNodeById(document, neighbourId);

                    // Проверяем, существует ли узел и соответствует ли его имя системы
                    if (neighbourNode != null && neighbourNode.SystemName == systemname)
                    {
                        // Здесь можно добавлять логику для работы с соседом, например, вывод его id 
                        //Console.WriteLine($"Обход элемента: {neighbourNode.ModelElementId}");

                        // Рекурсивно вызываем метод для соседнего узла 
                        neighbourNode.RecursiveTraversal(document, visitedIds);

                        // Здесь можно добавить логику для заполнения Connections, если необходимо
                        //FillConnectionsWithNeighbour(document,neighbourNode);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        // Метод для заполнения связей (Connections) с соседним узлом
        private void FillConnectionsWithNeighbour( Autodesk.Revit.DB.Document document, ModelNode neighbourNode)
        {
            // Реализуйте логику добавления связей
            // Например, добавьте neighbourNode в коллекцию Connections текущего узла
            Element element = document.GetElement(neighbourNode.ModelElementId);
            ConnectorSet connectorSet = GetConnectorSet(element); // Вынесение логики получения ConnectorSet в отдельный метод  
            if (connectorSet != null)
            {
                PopulateVertices(connectorSet); // Вынесение логики заполнения Vertices в отдельный метод 
                PopulateConnections(connectorSet);
            }
            
        }

        private ModelNode GetModelNodeById(Autodesk.Revit.DB.Document document, ElementId id)
        {
            Element element = document.GetElement(id);
            if (element != null && element.Category.Name != "Трубопроводная система")
            {

                ModelNode modelNode = new ModelNode(document, id);
                return modelNode;
            }
            else
            {
                return null; // Верните найденный узел или null, если он не найден
            }



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
        public static List<ModelNode> Sort(List<ModelNode> nodes)
        {
            // Подготовка структуры данных
            Dictionary<ElementId, int> inDegree = new Dictionary<ElementId, int>();
            Dictionary<ElementId, ModelNode> nodeLookup = new Dictionary<ElementId, ModelNode>();

            // Заполнение входной степени для каждого узла и создание словаря для поиска
            foreach (var node in nodes)
            {
                nodeLookup[node.ModelElementId] = node;
                inDegree[node.ModelElementId] = 0; // Изначально входная степень равна 0
            }

            foreach (var node in nodes)
            {
                foreach (var neighbour in node.Neighbour)
                {
                    if (inDegree.ContainsKey(neighbour))
                    {
                        inDegree[neighbour]++; // Увеличиваем входную степень для соседей
                    }
                }
            }

            // Используем очередь для хранения узлов с нулевой входной степенью
            Queue<ModelNode> zeroInDegreeQueue = new Queue<ModelNode>();
            foreach (var node in nodes)
            {
                if (inDegree[node.ModelElementId] == 0)
                {
                    zeroInDegreeQueue.Enqueue(node);
                }
            }

            List<ModelNode> sortedList = new List<ModelNode>();

            while (zeroInDegreeQueue.Count > 0)
            {
                var currentNode = zeroInDegreeQueue.Dequeue();
                sortedList.Add(currentNode);

                foreach (var neighbour in currentNode.Neighbour)
                {
                    inDegree[neighbour]--;

                    // Если входная степень соседа стала равной нулю, добавляем его в очередь
                    if (inDegree[neighbour] == 0 && nodeLookup.ContainsKey(neighbour))
                    {
                        zeroInDegreeQueue.Enqueue(nodeLookup[neighbour]);
                    }
                }
            }

            // Проверка на наличие цикла в графе
            if (sortedList.Count < nodes.Count)
            {
                throw new Exception("Graph has at least one cycle. Topological sorting is not possible.");
            }

            return sortedList; // Возвращаем отсортированный список
        }

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
            //Тут фильтруем системы по наличию в имени сокращения
            foreach (var sys in systems)
            {
                if (sys.Name.Contains(dataContext.ContextViewModel.SelectedSystemName))
                {
                    selectedsystems.Add(sys);
                }
            }
            
            string csvcontent = "";
            List<ModelNode> nodes = new List<ModelNode>();
            foreach (var sys in selectedsystems)
            {
               
                List<ModelNode> startconnectors = new List<ModelNode>();
                var connectors = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                                .WhereElementIsNotElementType()
                                .ToElementIds();
                var pipingNetwork = ((PipingSystem)sys).PipingNetwork;
                foreach(var connector in connectors)
                {
                    ModelNode model = new ModelNode(doc, connector);
                    nodes.Add(model);
                }
                foreach (Element element in pipingNetwork)
                {
                    ModelNode model = new ModelNode(doc, element.Id);
                    nodes.Add(model);
                }
            }
            
            foreach (var el in nodes)
            {
                string a = $"{el.ModelElementId};";
                foreach (var n in el.Neighbour)
                {
                    string b = $"{n};";
                    a += b;
                }
                csvcontent += a + "\n";
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
            



            /*int branchcounter = 0;
            foreach (var group in sysconnectors)
            {
                int counter2 = 0;
                foreach (var element in group)
                {
                    string a = $"{branchcounter};{counter2};{element}" + "\n";
                    csvcontent += a;
                    counter2++;
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

            }*/
            return Result.Succeeded;
        }
        
    }
}
