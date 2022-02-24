using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using FixedMath;
using RVO;
using Vector2 = RVO.Vector2;


[UpdateAfter(typeof(PathFindSystem))]
[UpdateBefore(typeof(RVO.RVOSystem))]
[DisableAutoCreation]
public class MoveSystem : WorkSystem
{
 
 

    public override void Work(){
        // Debug.Log("work");
        Entities.ForEach((Entity entity ,   DynamicBuffer<PathPosition> pathPositionBuffer,ref Agent agent, ref PathFollow pathFollow ) =>{
            if (pathFollow.pathIndex >= 0) {
                // Has path to follow
                PathPosition pathPosition = pathPositionBuffer[pathFollow.pathIndex];
            
                Vector2 targetPosition = new Vector2(pathPosition.position.x, pathPosition.position.y);
                Vector2 moveDir =  targetPosition - agent.position_;
                if(RVOMath.absSq(moveDir) != 0)
                    moveDir = RVOMath.normalize(moveDir);
                agent.prefVelocity_ = moveDir;

    

                if (RVOMath.absSq(agent.position_ - targetPosition) >= 0 && RVOMath.abs(agent.position_ - targetPosition)< FixedInt.half ) {
                    // Next waypoint
                    pathFollow.pathIndex--;
                    EntityCommandBuffer  ecb =  endFixedStepSimulationEntityCommandBufferSystem.CreateCommandBuffer() ;
                    ecb.SetComponent<PathFollow>( entity, pathFollow);;
                }
            }
            else{
                agent.prefVelocity_ = new Vector2(0,0);
            }
        }).WithoutBurst().Run();
     

    }

     
}