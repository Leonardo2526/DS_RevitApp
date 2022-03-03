﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using DS.Revit.Utils.MEP;
using DS.Revit.Utils;

namespace DS.Revit.MEPAutoCoordination.Offset
{

    class MovableElement
    {
        readonly XYZ MoveVector;

        public MovableElement(XYZ moveVector)
        {
            MoveVector = moveVector;
        }

        ConnectorUtils connectorUtils = new ConnectorUtils();
        ElementUtils elementUtils = new ElementUtils();

        public List<Element> MovableElements = new List<Element>();
        public List<MEPCurve> PotentialReducibleElements = new List<MEPCurve>();

        public bool IsMovableElementsCountValid { get; set; } = true;


        /// <summary>
        /// Check if current move vector is available for correct system connection.
        /// </summary>
        public bool GetMovableElements(XYZ moveVector)
        {
            List<Element> elements = new List<Element>();
            elements.Add(Data.Elem1Curve);
            List<Element> preElements = new List<Element>();

            SearchConnected(elements, preElements);


            return true;
        }

        /// <summary>
        /// Get elements obstructed for moving.
        /// </summary>
        void SearchConnected(List<Element> elements, List<Element> preElements)
        {

            List<Element> connectedToCurrent = new List<Element>();
            List<Element> elementsForSearch = new List<Element>();
            IEnumerable<ElementId> elementsIds = elements.Select(el => el.Id);
            IEnumerable<ElementId> preElementsIds = preElements.Select(el => el.Id);

            foreach (Element element in elements)
            {
                IEnumerable<ElementId> connectedElemIdsEnum = ConnectorUtils.GetConnectedElements(element).Select(el => el.Id);
                foreach (ElementId elId in connectedElemIdsEnum)
                {
                    if (!preElementsIds.Contains(elId))
                        connectedToCurrent.Add(Data.Doc.GetElement(elId));
                }
            }

            elementsForSearch = GetElementsForSearch(connectedToCurrent);

            if (elementsForSearch.Count > 0)
                SearchConnected(elementsForSearch, elements);
        }

        /// <summary>
        /// Get elements for next search step. Add to output if it are families or MEP elements with axis athwart to move vector. 
        /// If obstacles found, return null.
        /// </summary>
        List<Element> GetElementsForSearch(List<Element> elements)
        {
            List<Element> elementsForNewSearch = new List<Element>();

            foreach (Element element in elements)
            {
                MovableElementChecker movableElementChecker = new MovableElementChecker(MoveVector, element);

                Type type = element.GetType();
                if (type.ToString().Contains("System") | type.ToString().Contains("Insulation"))
                    continue;

                MovableElements.Add(element);

                if (movableElementChecker.GetTypeName() == "FamilyInstance")
                    elementsForNewSearch.Add(element);
                else
                {
                    movableElementChecker.GetData();
                    if (!movableElementChecker.CheckAngle())
                        PotentialReducibleElements.Add(element as MEPCurve);
                    else if (movableElementChecker.CheckAngle())
                        elementsForNewSearch.Add(element);
                }
            }

            return elementsForNewSearch;
        }       

        public List<int> GetCollisions()
        {
            GetMovableElements(MoveVector);

            if (MovableElements.Count == 0)
                return new List<int>();


            if (MovableElements.Count > 150)
            {
                IsMovableElementsCountValid = false;
                return new List<int>();
            }

            List<Solid> movableElementsSolids = ElementUtils.GetSolidsOfElements(MovableElements);

            IBoundingBoxFilter elementsBoundingBoxFilter = new SolidsBoundingBox(movableElementsSolids);

            BoundingBoxFilter boundingBoxFilter = new BoundingBoxFilter();
            BoundingBoxIntersectsFilter boundingBoxIntersectsFilter =
                boundingBoxFilter.GetBoundingBoxFilter(elementsBoundingBoxFilter);

            IMovableElemCollision movableElemCollision =
                   new MovableElementCollision(MovableElements, boundingBoxIntersectsFilter, movableElementsSolids, null);

            List<int> collisions = movableElemCollision.GetCollisions();

            if (!movableElemCollision.IsModelsElementsCountValid)
            {
                IsMovableElementsCountValid = false;
                return new List<int>();
            }

            return collisions;
        }

        public List<int> GetCollisionsByTransform(List<Element> movableElements, XYZ moveVector, List<Element> excludedElements)
        {
            List<Solid> movableElementsSolids = ElementUtils.GetTransformSolidsOfElements(movableElements, moveVector);

            IBoundingBoxFilter elementsBoundingBoxFilter = new SolidsBoundingBox(movableElementsSolids);

            BoundingBoxFilter boundingBoxFilter = new BoundingBoxFilter();
            BoundingBoxIntersectsFilter boundingBoxIntersectsFilter =
                boundingBoxFilter.GetBoundingBoxFilter(elementsBoundingBoxFilter);

            IMovableElemCollision movableElemCollision =
                   new MovableElementCollision(MovableElements, boundingBoxIntersectsFilter, movableElementsSolids, excludedElements);

            return movableElemCollision.GetCollisions();
        }

        public Dictionary<MEPCurve, XYZ> GetStaticCenterPoints()
        {
            var staticCenterPoints = new Dictionary<MEPCurve, XYZ>();

            ElementUtils.GetPoints(Data.Elem1Curve, out XYZ startPoint, out XYZ endPoint, out XYZ centerPoint);

            foreach (var mEPCurve in PotentialReducibleElements)
            {
                ElementUtils.GetPoints(mEPCurve, out XYZ p1, out XYZ p2, out XYZ cp);
                if (p1.DistanceTo(centerPoint) < p2.DistanceTo(centerPoint))
                    staticCenterPoints.Add(mEPCurve, p2);
                else
                    staticCenterPoints.Add(mEPCurve, p1);

            }

            return staticCenterPoints;
        }

        public bool CheckCurrentCollisions(
           MovableElement movableElement, XYZ moveVector, int startColllisionsCount, Dictionary<MEPCurve, XYZ> staticCenterPoints, List<Element> excludedElements)
        {
            if (staticCenterPoints.Count == 0)
                return true;

            List<Element> checkedElements = new List<Element>();
            List<ElementId> potentialObstacledElementsIds =
                movableElement.PotentialReducibleElements.Select(el => el.Id).ToList();

            foreach (Element element in movableElement.MovableElements)
            {
                if (!potentialObstacledElementsIds.Contains(element.Id))
                    checkedElements.Add(element);
            }

            List<int> currentMovableElementCollisions =
                        movableElement.GetCollisionsByTransform(checkedElements, moveVector, excludedElements);

            int currentCollisionsCount = 0;
            foreach (int c in currentMovableElementCollisions)
                currentCollisionsCount += c;

            if (staticCenterPoints.Count > 0)
            {
                int reducibleElementsCollision = ReducibleCurve.GetCollisions(staticCenterPoints, movableElement, moveVector, excludedElements);
                currentCollisionsCount += reducibleElementsCollision;
            }

            if (currentCollisionsCount != 0)
                return false;

            //if (currentCollisionsCount > startColllisionsCount)
            //    return false;

            return true;
        }
    }


}
