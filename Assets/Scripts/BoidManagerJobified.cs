
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Jobified
{
    using Unity.Mathematics;

    [BurstCompile]
    public class BoidManagerJobified : MonoBehaviour
    {
        public List<BoidGroup> flocks = new List<BoidGroup>();
        public Boid BoidPrefab;
        public int numGroups = 3;
        public int boidsPerGroup = 20;
        public float radius = 5;
        public float bigRadius = 20;
        public List<float> weights = new List<float>();
        public Bounds bounds;

        private NativeArray<BoidDataJobs> boidData = new NativeArray<BoidDataJobs>();
        private NativeArray<float> weightsNativeArray;

        private BoidJob boidJob;

        private void Start()
        {
            for (int i = 0; i < numGroups; i++)
            {
                flocks.Add(CreateGroup(i, boidsPerGroup));
            }

            boidData = new NativeArray<BoidDataJobs>(boidsPerGroup, Allocator.Persistent);

            weightsNativeArray = new NativeArray<float>(weights.Count, Allocator.Persistent);
            weightsNativeArray.CopyFrom(weights.ToArray());
        }


        private void FixedUpdate()
        {
            foreach (var boidGroup in flocks)
            {
                int i = 0;
                foreach (Boid b in boidGroup.boids)
                {
                    BoidDataJobs data = new BoidDataJobs();
                    data.curDir = b.transform.forward;// b.data.curDir;
                    data.pos = b.transform.position;//.pos;
                    data.desiredDir = b.data.desiredDir;
                    boidData[i] = data;
                    i++;
                }
            }
            boidJob = new BoidJob()
            {
                BigRadius = bigRadius,
                BorderCenter = bounds.center,
                BorderSize = bounds.size,
                Data = boidData,
                NumBoids = boidsPerGroup,
                Radius = radius,
                Weights = weightsNativeArray
            };
            var jobHandle = boidJob.Schedule(boidsPerGroup, 256);
            jobHandle.Complete();
            foreach (var boidGroup in flocks)
            {
                //boidGroup.UpdateBoids(weights, bounds);
                int i = 0;
                foreach (Boid b in boidGroup.boids)
                {
                    float3 desiredDir = boidJob.Data[i].desiredDir;
                    Boid.BoidData data = new Boid.BoidData()
                    {
                        //desiredDir = new Vector3(desiredDir.x, desiredDir.y, desiredDir.z),
                        desiredDir = boidJob.Data[i].desiredDir

                    };

                    b.UpdateData(data);
                    i++;
                }
            }
        }
        public List<Boid> CreateBoidsList(int amount)
        {
            List<Boid> boids = new List<Boid>();
            for (int i = 0; i < amount; i++)
            {
                Boid boid = Instantiate(BoidPrefab, transform.position + UnityEngine.Random.insideUnitSphere * 2f, Quaternion.identity);
                boids.Add(boid);
            }
            return boids;
        }

        public BoidGroup CreateGroup(int id, int numBoids)
        {
            var boids = CreateBoidsList(numBoids);
            return new BoidGroup(id, boids);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    [BurstCompile]
    public struct BoidDataJobs
    {
        public float3 pos;
        public float3 desiredDir;
        public float3 curDir;
    }

    [BurstCompile]
    public struct BoidJob : IJobParallelFor
    {
        public NativeArray<BoidDataJobs> Data;
        public NativeArray<float> Weights;
        public int NumBoids;
        public float Radius;
        public float BigRadius;
        public float3 BorderCenter;
        public float3 BorderSize;

        float sqrmag(float3 p1, float3 p2)
        {
            return (p1.x - p2.x) * (p1.x - p2.x) +
            (p1.y - p2.y) * (p1.y - p2.y) +
            (p1.z - p2.z) * (p1.z - p2.z);
        }

        bool IsPointInBounds(float3 p, float3 center, float3 size)
        {
            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;
            float halfZ = size.z * 0.5f;
            return (p.x > center.x - halfX && p.x < center.x + halfX &&
                p.y > center.y - halfY && p.y < center.y + halfY &&
                p.z > center.z - halfZ && p.z < center.z + halfZ);
        }

        public void Execute(int index)
        {
            float3 coh = new float3(0, 0, 0);
            float3 sep = new float3(0, 0, 0);
            float3 align = new float3(0, 0, 0);
            float3 border = new float3(0, 0, 0);
            int numSep = 0;
            int numAlign = 0;
            int numCoh = 0;
            for (int i = 0; i < NumBoids; i++)
            {
                float dist = sqrmag(Data[i].pos, Data[index].pos);
                if (dist <= Radius * Radius && dist > 0)
                {
                    sep += (Data[index].pos - Data[i].pos) / dist;
                    numSep++;
                }
                if (dist <= BigRadius * BigRadius && dist > 0)
                {
                    align += Data[i].curDir;
                    numAlign++;
                    coh += Data[i].pos;
                    numCoh++;
                }
            }

            if (numCoh != 0)
            {
                coh = math.normalize((coh / numCoh) - Data[index].pos) * Weights[0];
            }
            if (numSep != 0)
            {
                sep = math.normalize(sep / numSep) * Weights[1];
            }
            if (numAlign != 0)
            {
                align = math.normalize((align - Data[index].curDir) / numAlign) * Weights[2];
            }
            //Apply Bounds
            if (!IsPointInBounds(Data[index].pos, BorderCenter, BorderSize))
            {
                border = math.normalize(BorderCenter - Data[index].pos) * Weights[3];
            }

            BoidDataJobs data = Data[index];
            data.desiredDir = (coh + sep + align + border) / 4.0f;
            Data[index] = data;
        }
    }
}

