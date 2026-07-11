/*
    Copyright (c) 2019 The LibDescent Team

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

//Enable to diagonse the picking of an initial splitter for a subobject.
//#define DEBUG_SPLITTER_DIAGONISTICS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LibDescent.Data
{
    public enum BSPClassification
    {
        Front,
        Back,
        OnPlane,
        Spanning,
    }
    public enum BSPNodeType
    {
        Node,
        Leaf,
    }
    public class BSPVertex
    {
        public Vector3 Point;
        public Vector3 UVs;
        public BSPClassification Classification;
    }

    public class BSPNode
    {
        public BSPFace Splitter;
        public Vector3 Point;
        public Vector3 Normal;
        public BSPNode Front;
        public BSPNode Back;
        public List<BSPFace> faces = new List<BSPFace>();
        public BSPNodeType type;
    }

    public class BSPFace
    {
        public Vector3 Point;
        public Vector3 Normal;
        public int TextureID;
        public int Color;
        public List<BSPVertex> Points = new List<BSPVertex>();
        public BSPClassification Classification;

        public BSPFace()
        {
            TextureID = -1;
        }

        public void CalculateCenter()
        {
            //Point = new Vector3(Points.Average(p => p.Point.X), Points.Average(p => p.Point.Y), Points.Average(p => p.Point.Z));
            Point = Points[0].Point;
        }

        public void CalculateNormal()
        {
            this.Normal = new Vector3(0.0f);

            for (int i = 0; i < this.Points.Count; i++)
            {
                int j = i + 1;

                if (j == Points.Count)
                {
                    j = 0;
                }

                Normal.X -= (((Points[i].Point.Z) + (Points[j].Point.Z)) * ((Points[j].Point.Y) - (Points[i].Point.Y)));
                Normal.Y -= (((Points[i].Point.X) + (Points[j].Point.X)) * ((Points[j].Point.Z) - (Points[i].Point.Z)));
                Normal.Z -= (((Points[i].Point.Y) + (Points[j].Point.Y)) * ((Points[j].Point.X) - (Points[i].Point.X)));
            }

            float l = Normal.Length();

            if (l > 0.0f)
            {
                Normal.X /= l;
                Normal.Y /= l;
                Normal.Z /= l;
            }
        }
    }

    public class BSPTree
    {
#if DEBUG_SPLITTER_DIAGONISTICS
        int recursionLevel = 0;
#endif

        public bool PointOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNorm)
        {
            Vector3 localPoint = point - planePoint;
            return Math.Abs(Vector3.Dot(localPoint, planeNorm)) < .001;
        }

        public bool PointInFront(Vector3 point, Vector3 planePoint, Vector3 planeNorm)
        {
            Vector3 localPoint = point - planePoint;
            return Vector3.Dot(localPoint, planeNorm) > 0;
        }

        public void BuildTree(BSPNode node, List<BSPFace> faces)
        {
#if DEBUG_SPLITTER_DIAGONISTICS
            if (recursionLevel == 0)
            {
                Console.WriteLine("----------------------------------------------");
                Console.WriteLine("STARTING");
                Console.WriteLine("----------------------------------------------");
            }
            recursionLevel++;
#endif
            List<BSPFace> frontList = new List<BSPFace>();
            List<BSPFace> backList = new List<BSPFace>();

            node.Splitter = FindSplitter(faces, ref node.Point, ref node.Normal);

            if (node.Splitter == null) //If a splitter wasn't found, this set of faces is convex
            {
                node.faces = faces;
                node.type = BSPNodeType.Leaf;
            }
            else //A splitter is known, so do any needed splits and recurse
            {
                foreach (BSPFace face in faces)
                {
                    if (face != node.Splitter) //splitter is classified and added later. 
                    {
                        ClassifyFace(face, node.Splitter.Point, node.Splitter.Normal);
                        //else
                        //    //[ISB] Fix bug with splitters ending up on both sides. Doom puts them in front implicity
                        //    face.Classification = BSPClassification.Front;

                        switch (face.Classification)
                        {
                            case BSPClassification.Front:
                                frontList.Add(face);
                                break;
                            case BSPClassification.Back:
                                backList.Add(face);
                                break;
                            case BSPClassification.Spanning:
                                BSPFace frontFace = new BSPFace();
                                BSPFace backFace = new BSPFace();
                                if (face.TextureID == -1) // colored face
                                    frontFace.Color = backFace.Color = face.Color;

                                SplitPolygon(face, node.Splitter.Point, node.Splitter.Normal, ref frontFace, ref backFace);


                                frontList.Add(frontFace);
                                backList.Add(backFace);
                                break;

                            default:
                                throw new Exception("BSPTree::BuildTree: Face has invalid classification.");
                        }
                    }
                }

                //Where does the splitter go?
                //Try it in both the front and back. Whichever one generates the highest score fails.
                int frontScore = 0;
                int backScore = 0;
                int tempScore;
                List<BSPFace> faceTemp;

                if (frontList.Count > 0)
                {
                    faceTemp = new List<BSPFace>(frontList);
                    faceTemp.Add(node.Splitter);
                    foreach (BSPFace face in faceTemp)
                    {
                        tempScore = EvalulateSplitter(faceTemp, face);
                        if (tempScore != int.MaxValue && tempScore > frontScore)
                            frontScore = tempScore;
                    }
                }
                else
                    frontScore = int.MaxValue;

                if (backList.Count > 0)
                {
                    faceTemp = new List<BSPFace>(backList);
                    faceTemp.Add(node.Splitter);
                    foreach (BSPFace face in faceTemp)
                    {
                        tempScore = EvalulateSplitter(faceTemp, face);
                        if (tempScore != int.MaxValue && tempScore > backScore)
                            backScore = tempScore;
                    }
                }
                else
                    backScore = int.MaxValue;

                if (frontScore > backScore)
                    frontList.Add(node.Splitter);
                else
                    backList.Add(node.Splitter);

#if DEBUG_SPLITTER_DIAGONISTICS
                if (recursionLevel <= 2)
                    Console.WriteLine("Frontlist:{0} Backlist:{1}, fs<bs:{2}", frontList.Count, backList.Count, frontScore < backScore);
#endif

                if (frontList.Count > 0)
                {
                    BSPNode newNode = new BSPNode();
                    newNode.type = BSPNodeType.Node;

#if DEBUG_SPLITTER_DIAGONISTICS
                    if (recursionLevel <= 1)
                        Console.WriteLine("Doing front.");
#endif
                    BuildTree(newNode, frontList);
                    node.Front = newNode;
                }

                if (backList.Count > 0)
                {
                    BSPNode newNode = new BSPNode();
                    newNode.type = BSPNodeType.Node;
#if DEBUG_SPLITTER_DIAGONISTICS
                    if (recursionLevel <= 1)
                        Console.WriteLine("Doing back.");
#endif
                    BuildTree(newNode, backList);
                    node.Back = newNode;
                }
            }
#if DEBUG_SPLITTER_DIAGONISTICS
            recursionLevel--;
#endif
        }

        public void ClassifyPoint(BSPVertex vert, Vector3 planePoint, Vector3 planeNorm)
        {
            if (PointOnPlane(vert.Point, planePoint, planeNorm)) vert.Classification = BSPClassification.OnPlane;
            else if (PointInFront(vert.Point, planePoint, planeNorm)) vert.Classification = BSPClassification.Front;
            else vert.Classification = BSPClassification.Back;
        }

        public void ClassifyFace(BSPFace face, Vector3 planePoint, Vector3 planeNorm)
        {
            face.Classification = BSPClassification.OnPlane;

            foreach (BSPVertex point in face.Points)
            {
                ClassifyPoint(point, planePoint, planeNorm);

                if (point.Classification != face.Classification)
                {
                    if (face.Classification == BSPClassification.OnPlane)
                    {
                        face.Classification = point.Classification;
                    }
                    else if (point.Classification != BSPClassification.OnPlane)
                    {
                        face.Classification = BSPClassification.Spanning;
                        return;
                    }
                }
            }
            if (face.Classification == BSPClassification.OnPlane) //Place coplanar faces on the front side of the plane if it is facing the same direction
            {
                if (Vector3.Dot(face.Normal, planeNorm) >= 0)
                    face.Classification = BSPClassification.Front;
                else
                    face.Classification = BSPClassification.Back;
            }
        }

        public int EvalulateSplitter(List<BSPFace> faces, BSPFace splitter)
        {
            int numFront = 0, numBack = 0, numSplits = 0;
            //int numVertsFront = 0, numVertsBack = 0;
            foreach (BSPFace face in faces)
            {
                if (face == splitter) //Bugfix? Splitter is always on front side. 
                {
                    numFront++;
                    continue;
                }
                ClassifyFace(face, splitter.Point, splitter.Normal);
                switch (face.Classification)
                {
                    case BSPClassification.Front:
                        numFront++;
                        break;
                    case BSPClassification.Back:
                        numBack++;
                        break;
                    case BSPClassification.Spanning:
                        numFront++;
                        numBack++;
                        numSplits++;
                        break;
                }

                /*foreach (BSPVertex vert in face.Points)
                {
                    if (vert.Classification == BSPClassification.Back)
                        numVertsBack++;
                    else if (vert.Classification == BSPClassification.Front)
                        numVertsFront++;
                    else if (vert.Classification == BSPClassification.OnPlane)
                    {
                        numVertsFront++;
                    }
                }*/
            }

            int newFaces = (numFront + numBack) - faces.Count;

            if (numSplits == 0 && (numFront == 0 || numBack == 0)) //If everything is on one side of this splitter, it has no value. 
            {
#if DEBUG_SPLITTER_DIAGONISTICS
                if (recursionLevel <= 2)
                    Console.WriteLine("\tSplitter is not needed.");
#endif
                return int.MaxValue;
            }
#if DEBUG_SPLITTER_DIAGONISTICS
            if (recursionLevel <= 2)
                Console.WriteLine("\tSplitter score is {0} (Base:{1}, f:{2} b:{3} s:{4})", Math.Abs(numFront - numBack) + (numSplits * 8), faces.Count, numFront, numBack, numSplits);
#endif
            return Math.Abs(numFront - numBack) + (numSplits * 8);
            //return Math.Abs(numVertsFront - numVertsBack) + (numSplits * 6);
            //return Math.Max(numFront, numBack) + (newFaces * 24);
        }

        public BSPFace FindSplitter(List<BSPFace> faces, ref Vector3 planePoint, ref Vector3 planeNorm)
        {
            int bestScore = int.MaxValue;
            BSPFace bestFace = null;
            int score;
            foreach (BSPFace potential in faces)
            {
#if DEBUG_SPLITTER_DIAGONISTICS
                if (recursionLevel <= 2)
                    Console.WriteLine("Evalulating splitter {0}.", faces.IndexOf(potential));
#endif
                score = EvalulateSplitter(faces, potential);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFace = potential;
                }
            }
            if (bestFace != null)
            {
                planePoint = bestFace.Point;
                planeNorm = bestFace.Normal;
            }
#if DEBUG_SPLITTER_DIAGONISTICS
            if (recursionLevel <= 2)
            {
                if (bestFace != null)
                    Console.WriteLine("Best face is {0}.", faces.IndexOf(bestFace));
                else
                    Console.WriteLine("This object is convex.");
            }
#endif
            return bestFace;
        }

        public bool SplitEdge(Vector3 point1, Vector3 point2, Vector3 planePoint, Vector3 planeNorm, ref float percentage, ref Vector3 intersect)
        {
            Vector3 direction = point2 - point1;
            float lineLength = Vector3.Dot(direction, planeNorm);

            if (Math.Abs(lineLength) < 0.0001)
                return false;

            Vector3 L1 = planePoint - point1;
            float distFromPlane = Vector3.Dot(L1, planeNorm);
            percentage = distFromPlane / lineLength;

            if (percentage < 0) return false;
            if (percentage > 1) return false;
            intersect = point1 + (direction * percentage);
            return true;
        }

        public void SplitPolygon(BSPFace face, Vector3 planePoint, Vector3 planeNorm, ref BSPFace front, ref BSPFace back)
        {
            front.TextureID = face.TextureID;
            back.TextureID = face.TextureID;

            BSPVertex firstPoint = face.Points[0];
            if (firstPoint.Classification == BSPClassification.OnPlane)
            {
                front.Points.Add(firstPoint);
                back.Points.Add(firstPoint);
            }
            else if (firstPoint.Classification == BSPClassification.Front)
            {
                front.Points.Add(firstPoint);
            }
            else
            {
                back.Points.Add(firstPoint);
            }

            int current = 0;
            BSPVertex vert1, vert2;
            for (int i = 1; i < face.Points.Count + 1; i++)
            {
                if (i == face.Points.Count) current = 0;
                else current = i;

                vert1 = face.Points[i - 1];
                vert2 = face.Points[current];

                ClassifyPoint(vert2, planePoint, planeNorm);
                if (vert2.Classification == BSPClassification.OnPlane)
                {
                    front.Points.Add(vert2);
                    back.Points.Add(vert2);
                }
                else
                {
                    Vector3 intersect = new Vector3();
                    float percentage = 0.0f;

                    bool split = SplitEdge(vert1.Point, vert2.Point, planePoint, planeNorm, ref percentage, ref intersect);

                    if (split)
                    {
                        Vector3 texDelta = vert2.UVs - vert1.UVs;
                        BSPVertex newVert = new BSPVertex
                        {
                            Classification = BSPClassification.OnPlane,
                            Point = intersect,
                            UVs = texDelta * percentage + vert1.UVs
                        };

                        if (vert2.Classification == BSPClassification.Front)
                        {
                            back.Points.Add(newVert);
                            front.Points.Add(newVert);
                            front.Points.Add(vert2);
                        }
                        else if (vert2.Classification == BSPClassification.Back)
                        {
                            front.Points.Add(newVert);
                            back.Points.Add(newVert);
                            back.Points.Add(vert2);
                        }
                    }
                    else
                    {
                        if (vert2.Classification == BSPClassification.Front)
                        {
                            front.Points.Add(vert2);
                        }
                        else if (vert2.Classification == BSPClassification.Back)
                        {
                            back.Points.Add(vert2);
                        }
                    }
                }
            }
            //TODO: This isn't always accurate at extreme splits. 
            front.Normal = face.Normal;
            back.Normal = face.Normal;

            //front.CalculateNormal();
            front.CalculateCenter();

            //back.CalculateNormal();
            back.CalculateCenter();
        }
    }
}
