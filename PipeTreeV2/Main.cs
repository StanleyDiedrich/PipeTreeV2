﻿using System;
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

    public class ModelNode
    {
        public ElementId ModelElementId { get; set; }
        public int Counter { get; set; }
        public List<ElementId> Neighbour { get; set; } = new List<ElementId>();
        public int ElementCounter { get; set; }
        public Dictionary<ElementId, List<ElementId>> Connections { get; set; } = new Dictionary<ElementId, List<ElementId>>();

        public ModelNode(Autodesk.Revit.DB.Document document, ElementId elementId)
        {
            ModelElementId = elementId;

            Element element = document.GetElement(ModelElementId);
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
                        if (nextconnector.Owner is PipingSystem)
                        {
                            continue;
                        }
                        else if (!Neighbour.Contains(nextconnector.Owner.Id))
                        {
                            if (nextconnector.Direction == FlowDirectionType.Out || nextconnector.Direction == FlowDirectionType.Bidirectional)
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
        public void RecursiveTraversal(Autodesk.Revit.DB.Document document, List<ElementId> visitedIds)
        {
            // Добавляем текущий элемент в список посещенных
            visitedIds.Add(ModelElementId);

            // Обходим соседние элементы
            foreach (ElementId neighbourId in Neighbour)
            {
                if (!visitedIds.Contains(neighbourId))
                {
                    // Получаем модель узла по соседнему id (необходимо создать соответствующий метод)
                    ModelNode neighbourNode = GetModelNodeById(document, neighbourId);

                    if (neighbourNode != null)
                    {
                        // Здесь можно добавлять логику для работы с соседом, например, вывод его id
                        //Console.WriteLine($"Обход элемента: {neighbourNode.ModelElementId}");

                        // Рекурсивно вызываем метод для соседнего узла
                        neighbourNode.RecursiveTraversal(document, visitedIds);

                        // Вызов нового метода для заполнения Connections


                    }
                }
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
                //Тут фильтруем системы по наличию в имени сокращения
                foreach (var sys in systems)
                {
                    if (sys.Name.Contains(dataContext.ContextViewModel.SelectedSystemName))
                    {
                        selectedsystems.Add(sys);
                    }
                }

                foreach (var sys in selectedsystems)
                {
                    List<ModelNode> startconnectors = new List<ModelNode>();
                    List<List<ElementId>> visitednodessys = new List<List<ElementId>>();
                    var connectors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElementIds();
                    int counter = 1;
                    foreach (ElementId element in connectors)
                    {
                        if (doc.GetElement(element).Name.Contains("Виртуальный коннектор"))
                        {
                            ModelNode modelNode = new ModelNode(doc, element);
                            startconnectors.Add(modelNode);
                            counter++;
                        }
                    }
                    foreach (var startconnector in startconnectors)
                    {
                        List<ElementId> visitednodes = new List<ElementId>();
                        startconnector.RecursiveTraversal(doc, visitednodes);
                        visitednodessys.Add(visitednodes);
                    }
                    
                }









                return Result.Succeeded;
            }
        }
    }
}
