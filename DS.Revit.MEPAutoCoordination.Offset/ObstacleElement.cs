﻿using Autodesk.Revit.DB;
using DS.Revit.Utils.MEP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.Revit.MEPAutoCoordination.Offset
{
    class ObstacleElement
    {
        public static XYZ VectorForFamInst { get; set; }
        public static Element ElementToMove { get; set; }


        public static Element GetElementToMove(List<Element> MovableElements, Element ruducibleElement)
        {
            List<Element> famInstToMove = ConnectorUtils.GetConnectedFamilyInstances(ruducibleElement);

            for (int i = 0; i < famInstToMove.Count; i++)
            {
                for (int j = 0; j < MovableElements.Count; j++)
                {
                    if (famInstToMove[i].Id == MovableElements[j].Id)
                        continue;
                    else
                    {
                        ElementToMove = famInstToMove[i];
                        return ElementToMove;
                    }

                }
            }

            return null;
        }

        public static XYZ GetMoveVector(double curvelength, double MinCurveLength)
        {
            double deltaF = curvelength - MinCurveLength;
            double delta = UnitUtils.Convert(deltaF,
                                       DisplayUnitType.DUT_DECIMAL_FEET,
                                       DisplayUnitType.DUT_MILLIMETERS);

            PointUtils pointUtils = new PointUtils();
            XYZ newoffset = pointUtils.GetOffsetByMoveVector(Data.MoveVector, delta);

            VectorForFamInst = new XYZ(Data.MoveVector.X - newoffset.X, Data.MoveVector.Y - newoffset.Y, Data.MoveVector.Z - newoffset.Z);
            return VectorForFamInst;
        }
    }
}
