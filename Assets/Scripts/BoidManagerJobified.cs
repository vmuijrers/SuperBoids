
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
        private NativeArray<BoidDataJobs> outputBoidData = new NativeArray<BoidDataJobs>();
        private NativeArray<float> weightsNativeArray;

        private BoidJob boidJob;

        //Used for GPU Instancing
        public Mesh mesh;
        public Material material;
        private List<Matrix4x4> instanceMatrices = new List<Matrix4x4>();
        private const int maxInstancesPerBatch = 1023;

        private void Start()
        {

            for (int i = 0; i < numGroups; i++)
            {
                flocks.Add(CreateGroup(i, boidsPerGroup));
            }

            boidData = new NativeArray<BoidDataJobs>(boidsPerGroup, Allocator.Persistent);
            outputBoidData = new NativeArray<BoidDataJobs>(boidsPerGroup, Allocator.Persistent);

            weightsNativeArray = new NativeArray<float>(weights.Count, Allocator.Persistent);
            weightsNativeArray.CopyFrom(weights.ToArray());

            for(int i = 0; i < boidsPerGroup; i++)
            {
                instanceMatrices.Add(new Matrix4x4());
            }
        }

        private void OnDestroy()
        {
            if (boidData.IsCreated) { boidData.Dispose(); }
            if (outputBoidData.IsCreated) { outputBoidData.Dispose(); }
            if (weightsNativeArray.IsCreated) { weightsNativeArray.Dispose(); }
        }

        private void Update()
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
                InputData = boidData,
                OutputData = outputBoidData,
                NumBoids = boidsPerGroup,
                Radius = radius,
                Weights = weightsNativeArray
            };
            int numThreads = 256;
            var jobHandle = boidJob.Schedule(boidsPerGroup, Mathf.CeilToInt(boidsPerGroup / (float)numThreads));
            jobHandle.Complete();

            for (int i = 0; i < boidsPerGroup; i++)
            {
                boidData[i] = outputBoidData[i];
            }
        }

        private void LateUpdate()
        {
            foreach (var boidGroup in flocks)
            {
                int i = 0;
                foreach (Boid b in boidGroup.boids)
                {
                    float3 desiredDir = boidData[i].desiredDir;
                    //Boid.BoidData data = new Boid.BoidData()
                    //{
                    //    desiredDir = desiredDir

                    //};
                    b.data.desiredDir = desiredDir;
                    b.UpdateData(b.data);
                    Matrix4x4 matrix = Matrix4x4.TRS(b.transform.position, b.transform.rotation, b.transform.localScale * 10);
                    instanceMatrices[i] = matrix;
                    i++;
                }

            }
            for (int i = 0; i < instanceMatrices.Count; i += maxInstancesPerBatch)
            {
                int batchSize = Mathf.Min(maxInstancesPerBatch, instanceMatrices.Count - i);
                Graphics.DrawMeshInstanced(
                    mesh,
                    0,
                    material,
                    instanceMatrices.GetRange(i, batchSize).ToArray()
                );
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
        [ReadOnly] public NativeArray<float> Weights;
        [ReadOnly] public NativeArray<BoidDataJobs> InputData;
        public NativeArray<BoidDataJobs> OutputData;
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
            BoidDataJobs currentBoid = InputData[index];
            foreach (var boid in InputData)
            {
                float dist = sqrmag(boid.pos, currentBoid.pos);
                if (dist <= Radius * Radius && dist > 0)
                {
                    sep += (currentBoid.pos - boid.pos) / dist;
                    numSep++;
                }
                if (dist <= BigRadius * BigRadius && dist > 0)
                {
                    align += boid.curDir;
                    numAlign++;
                    coh += boid.pos;
                    numCoh++;
                }
            }

            if (numCoh != 0)
            {
                coh = math.normalize((coh / numCoh) - currentBoid.pos) * Weights[0];
            }
            if (numSep != 0)
            {
                sep = math.normalize(sep / numSep) * Weights[1];
            }
            if (numAlign != 0)
            {
                align = math.normalize((align - currentBoid.curDir) / numAlign) * Weights[2];
            }
            //Apply Bounds
            if (!IsPointInBounds(currentBoid.pos, BorderCenter, BorderSize))
            {
                border = math.normalize(BorderCenter - currentBoid.pos) * Weights[3];
            }

            currentBoid.desiredDir = (coh + sep + align + border) / 4.0f;
            OutputData[index] = currentBoid;
        }
    }
}

