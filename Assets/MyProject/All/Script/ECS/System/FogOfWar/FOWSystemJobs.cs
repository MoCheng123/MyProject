
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;

using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System;

using FixedMath;

public partial class FOWSystem
{
    [BurstCompile]
    public struct FreshJobParallel : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Color32> blurBuffer;
        [ReadOnly]
        public UnsafeList<int> lastVisiableArea;
        public void Execute(int index)
        {
            blurBuffer[lastVisiableArea[index]] = new Color32(0, 0, 0, 222);
            // foreach (var i in lastVisiableArea)
            // {

            //     blurBuffer[i] = new Color32(0, 0, 0, 252);

            // }

        }
    }


    [BurstCompile]
    public struct ComputeFogJob : IJob
    {
        [ReadOnly] public NativeArray<ObstacleVertice> obstacles_;
        [ReadOnly] public NativeArray<ObstacleVerticeTreeNode> obstacleTree_;
        [ReadOnly] public ObstacleVerticeTreeNode obstacleTreeRoot;

        [ReadOnly] public FOWUnit fowUnit;

        public UnsafeParallelHashSet<int>.ParallelWriter setParaWriter;



        public void Execute()
        {


            int range = fowUnit.range;
            NativeList<ObstacleVertice> obstacleNeighbors = new NativeList<ObstacleVertice>(Allocator.Temp);

            GetRangeObstacleVertices(fowUnit.position, obstacleTreeRoot, fowUnit.range * fowUnit.range, obstacleNeighbors);
            NativeArray<int> unitDirsSign = new NativeArray<int>(obstacleNeighbors.Length, Allocator.Temp);
            CalculateUnitDirs(obstacleNeighbors, unitDirsSign);

            for (int i = -range; i <= range; i++)
                for (int j = -range; j <= range; j++)
                {
                    if (!CheckInRange(i, j, range)) continue;
                    var currentGridPos = fowUnit.position.ConvertToint2() + new int2(i, j);
                    if (!CheckUnVisiable(currentGridPos, obstacleNeighbors, unitDirsSign))
                    {
                        var index = GridSystem.GetGridIndexInFOW(currentGridPos);
                        setParaWriter.Add(index);
                        // blurBuffer[index] = new Color32(0, 0, 0, 0);
                    }
                }
            obstacleNeighbors.Dispose();

        }



        /// <summary>
        /// return true, if the grid is unvisiable
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <param name="obstacleNeighbors"></param>
        /// <returns></returns>
        private bool CheckUnVisiable(FixedVector2 gridPosition, NativeList<ObstacleVertice> obstacleNeighbors, NativeArray<int> unitDirsSign)
        {
            //从后往前遍历
            int from;
            int to;
            to = obstacleNeighbors.Length - 1;
            from = obstacleNeighbors.Length;


            while (to >= 0)
            {

                if (obstacleNeighbors[to].verticeId_ >= 0)
                {
                    to--;
                    continue;
                }
                NativeSlice<ObstacleVertice> tempOnstacle = new NativeSlice<ObstacleVertice>(obstacleNeighbors, to + 1, from - to - 1);
                NativeSlice<int> tempSign = new NativeSlice<int>(unitDirsSign, to + 1, from - to - 1);

                if (!CheckSingleObstacleVisiable(gridPosition, tempOnstacle, tempSign))
                {
                    return true;
                }
                from = to;

                to--;

            }
            return false;



        }

        private bool CheckSingleObstacleVisiable(FixedVector2 gridPosition, NativeSlice<ObstacleVertice> tempOnstacle, NativeSlice<int> tempSign)
        {
            for (int i = 0; i < tempOnstacle.Length; i++)
            {

                if (CheckIntheSameLeftAsUnit(tempOnstacle[i].direction_, tempOnstacle[i].point_, gridPosition, tempSign[i]))
                {
                    return true;
                }
            }
            return false;
        }
        private void CalculateUnitDirs(NativeList<ObstacleVertice> obstacleNeighbors, NativeArray<int> unitDirsSign)
        {
            for (int i = 0; i < obstacleNeighbors.Length; i++)
            {
                unitDirsSign[i] = FixedCalculate.Det(obstacleNeighbors[i].direction_, fowUnit.position - obstacleNeighbors[i].point_).sign;
       

            }
        }

        private bool CheckIntheSameLeftAsUnit(FixedVector2 obstacleVerticeDir, FixedVector2 obstacleVerticePoint, FixedVector2 gridPos, int sign)
        {
            var currentGridDir = gridPos - obstacleVerticePoint;
    

            return FixedCalculate.Det(obstacleVerticeDir, currentGridDir).sign == sign;
 
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public bool CheckInRange(int x, int y, int range) => x * x + y * y < range * range;


        public Color32 ChangeColor(Color32 before, char mark, int value)
        {
            switch (mark)
            {
                case 'r':
                    before.r = (byte)value;

                    break;
                case 'g':
                    before.g = (byte)value;
                    break;
                case 'b':
                    before.b = (byte)value;
                    break;
                case 'a':
                    before.a = (byte)value;
                    break;
            }
            return before;
        }


        private void GetRangeObstacleVertices(FixedVector2 fOWUnitPosition, ObstacleVerticeTreeNode node, FixedInt rangeSq, NativeList<ObstacleVertice> obstacleNeighbors)
        {
            if (node.obstacleVertice_Index == -1) return;
            ObstacleVertice obstacle1 = obstacles_[node.obstacleVertice_Index];
            ObstacleVertice obstacle2 = obstacles_[obstacle1.next_];

            FixedInt agentLeftOfLine = FixedCalculate.LeftOf(obstacle1.point_, obstacle2.point_, fOWUnitPosition);

            if (agentLeftOfLine >= 0)
            {
                if (node.left_index != -1) GetRangeObstacleVertices(fOWUnitPosition, obstacleTree_[node.left_index], rangeSq, obstacleNeighbors);
            }
            else
            {
                if (node.right_index != -1) GetRangeObstacleVertices(fOWUnitPosition, obstacleTree_[node.right_index], rangeSq, obstacleNeighbors);
            }
            // ComputeObstacleNeighbor(obstacles,obstacleTree, agentLeftOfLine >= 0 ? obstacleTree[node.left_index] : obstacleTree[node.right_index]  , agent, ref rangeSq, obstacleNeighbors);

            FixedInt distSqLine = FixedCalculate.Square(agentLeftOfLine) / FixedCalculate.Square(obstacle2.point_ - obstacle1.point_);

            if (distSqLine < rangeSq)
            {
                if (agentLeftOfLine < 0)
                {
                    /*
                        * Try obstacle at this node only if agent is on right side of
                        * obstacle (and can see obstacle).
                        */
                    InsertObstacleNeighbor(fOWUnitPosition, obstacle1, obstacleNeighbors, rangeSq);
                    // agent.insertObstacleNeighbor(node.obstacle_, rangeSq);
                }

                /* Try other side of line. */
                if (agentLeftOfLine >= 0)
                {
                    if (node.right_index != -1) GetRangeObstacleVertices(fOWUnitPosition, obstacleTree_[node.right_index], rangeSq, obstacleNeighbors);
                }
                else
                {
                    if (node.left_index != -1) GetRangeObstacleVertices(fOWUnitPosition, obstacleTree_[node.left_index], rangeSq, obstacleNeighbors);
                }

            }

        }

        private void InsertObstacleNeighbor(FixedVector2 fOWUnitPosition, ObstacleVertice obstacle, NativeList<ObstacleVertice> obstacleNeighbors_, FixedInt rangeSq)
        {
            ObstacleVertice nextObstacle = obstacles_[obstacle.next_];

            FixedInt distSq = FixedCalculate.DistSqPointLineSegment(obstacle.point_, nextObstacle.point_, fOWUnitPosition);


            if (distSq < rangeSq)
            {
                if (obstacleNeighbors_.Contains(obstacle))
                {
                    var index = obstacleNeighbors_.IndexOf(obstacle);

                    obstacleNeighbors_.Add(new ObstacleVertice());
                    for (int i = obstacleNeighbors_.Length - 1; i > index; i--)
                    {
                        obstacleNeighbors_[i] = obstacleNeighbors_[i - 1];
                    }
                    obstacleNeighbors_[index] = obstacle;
                }
                else
                {
                    //     //用-2来分割不同的 obstacle块
                    obstacleNeighbors_.Add(new ObstacleVertice { verticeId_ = -2 });
                    obstacleNeighbors_.Add(obstacle);
                }



            }

        }





    }











    [BurstCompile]
    private struct SetFogPixelJobParallel : IJobParallelFor
    {

        public UnsafeList<int>.ParallelWriter lastVisiableArea;
        [DeallocateOnJobCompletion]
        public NativeArray<int> visiableAreaArr;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<Color32> blurBuffer;



        public void Execute(int index)
        {



            lastVisiableArea.AddNoResize(visiableAreaArr[index]);

            blurBuffer[visiableAreaArr[index]] = new Color32(0, 0, 0, 0);

        }
    }





}
