using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    public List<BoidGroup> flocks = new List<BoidGroup>();
    public Boid BoidPrefab;
    public int numGroups = 3;
    public int boidsPerGroup = 20;
    public float radius = 5;
    public List<float> weights = new List<float>();
    public Bounds bounds;
    public ComputeShader computeShader;
    ComputeBuffer buffer;

    // Start is called before the first frame update
    void Start()
    {
        for(int i = 0; i <  numGroups; i++)
        {
            flocks.Add(CreateGroup(i, boidsPerGroup));
        }

        buffer = new ComputeBuffer(boidsPerGroup, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Boid.BoidData)));
        computeShader.SetBuffer(0, "Data", buffer);

        computeShader.SetInt("NumBoids", boidsPerGroup);
        computeShader.SetFloat("Radius", radius);
        computeShader.SetVector("BorderCenter", new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, 0));
        computeShader.SetVector("BorderSize", new Vector4(bounds.size.x, bounds.size.y, bounds.size.z, 0));
    }

    // Update is called once per frame
    void Update()
    {

        //Fill the buffer
        Boid.BoidData[] dataList = new Boid.BoidData[boidsPerGroup];
        foreach(var boidGroup in flocks)
        {
            //boidGroup.UpdateBoids(weights, bounds);
            int i = 0;
            foreach (Boid b in boidGroup.boids)
            {
                Boid.BoidData data = new Boid.BoidData();
                data.curDir = b.transform.forward;// b.data.curDir;
                data.pos = b.transform.position;//.pos;
                data.desiredDir = b.data.desiredDir;
                dataList[i] = data;
                i++;
            }
        }
        computeShader.SetFloats("Weights", weights.ToArray());
        buffer.SetData(dataList);
        //Dispatch to compute shader
        int numThreads = 256;
        int numGroups = Mathf.CeilToInt(boidsPerGroup / (float)numThreads);
        computeShader.Dispatch(0, numGroups, 1, 1);

        //Read from shader
        buffer.GetData(dataList);
        foreach (var boidGroup in flocks)
        {
            //boidGroup.UpdateBoids(weights, bounds);
            int i = 0;
            foreach (Boid b in boidGroup.boids)
            {
                b.UpdateData(dataList[i]);
                i++;
            }
        }
    }

    private void OnDestroy()
    {
        if(buffer != null)
        {
            buffer.Release();
        }
    }


    public BoidGroup CreateGroup(int id, int numBoids)
    {
        List<Boid> boids = new List<Boid>();
        for(int i = 0 ; i < numBoids; i++)
        {
            Boid boid = Instantiate(BoidPrefab, transform.position + Random.insideUnitSphere * 2f, Quaternion.identity);
            boids.Add(boid);
        }
        return new BoidGroup(id, boids);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}

[System.Serializable]
public class BoidGroup
{
    public int ID;
    public List<Boid> boids = new List<Boid>();

    public BoidGroup(int ID, List<Boid> boids)
    {
        this.ID = ID;
        this.boids = boids;
    }

    public void UpdateBoids(List<float> weights, Bounds bounds)
    {
        foreach(Boid b in boids)
        {
            b.UpdateBoid(boids, weights, bounds);
        }
    }
}
