using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Transforms;
using System;
using UnityEngine.UI;
using UnityEngine.Profiling;

public partial class AgentSystem : WorkSystem
{



    public override void Work()
    {
        if (!ShouldRunSystem()) return;




        #region  updateAgentJob
        Profiler.BeginSample("AgentStart");
        var kDTreeSystem = World.GetExistingSystem<KDTreeSystem>();
        var agents_ = kDTreeSystem.agents_;
        var agentTree_ = kDTreeSystem.agentTree_;
        var obstacles_ = kDTreeSystem.obstacleVertices_;
        var obstacleTree_ = kDTreeSystem.obstacleVerticesTree_;
        var obstacleTreeRoot = kDTreeSystem.obstacleVerticesTreeRoot;

        // Debug.Log(string.Format("{0}  ", DateTime.Now));



        var ecbPara = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        NativeList<JobHandle> jobHandleList = new NativeList<JobHandle>(Allocator.Temp);

        Profiler.EndSample();
        Entities.ForEach((Entity entity, int entityInQueryIndex, in Agent agent) =>
        {
            UpdateAgentJob updateAgentJob = new UpdateAgentJob
            {
                // newVelocity = newVelocity,
                // rangeNeighbors = rangeNeighbors,
                // enemyUnit = enemyUnit,
                entity = entity,
                agent = agent,
                agents = agents_,
                agentTree = agentTree_,
                obstacles = obstacles_,
                obstacleTree = obstacleTree_,
                obstacleTreeRoot = obstacleTreeRoot,
                indexInEntityQuery = entityInQueryIndex,
                ecbPara = ecbPara

            };




            jobHandleList.Add(updateAgentJob.Schedule());

        }).WithoutBurst().Run();
        JobHandle.CompleteAll(jobHandleList);

        #endregion

        jobHandleList.Dispose();



    }




}
