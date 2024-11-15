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
    public float bigRadius = 20;
    public List<float> weights = new List<float>();
    public Bounds bounds;
    public ComputeShader computeShader;
    private ComputeBuffer buffer;
    private int numThreads = 256;
    private int boidIncrease = 128;

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
        computeShader.SetVector("BorderCenter", new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, 0));
        computeShader.SetVector("BorderSize", new Vector4(bounds.size.x, bounds.size.y, bounds.size.z, 0));
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            boidsPerGroup += boidIncrease;
            flocks[0].AddBoids(CreateBoidsList(boidIncrease));
            if (buffer != null)
            {
                buffer.Release();
            }
            computeShader.SetInt("NumBoids", boidsPerGroup);
            buffer = new ComputeBuffer(boidsPerGroup, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Boid.BoidData)));
            computeShader.SetBuffer(0, "Data", buffer);
        }

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
        float[] vecs = new float[weights.Count * 4];
        for(int i = 0; i < weights.Count; i++)
        {
            vecs[i * 4] = weights[i];
            for(int j = 1; j <= 3; j++)
            {
                vecs[i * 4 + j] = 0;
            }
        }
        computeShader.SetFloat("Radius", radius);
        computeShader.SetFloat("BigRadius", bigRadius);
        computeShader.SetFloats("Weights", vecs);
        buffer.SetData(dataList);
        //Dispatch to compute shader
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

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 50), "coh: " + weights[0]);
        GUI.Label(new Rect(10, 30, 200, 50), "sep: " + weights[1]);
        GUI.Label(new Rect(10, 50, 200, 50), "ali: " + weights[2]);
        GUI.Label(new Rect(10, 70, 200, 50), "bor: " + weights[3]);
        GUI.Label(new Rect(10, 90, 200, 50), "rad: " + radius);
        GUI.Label(new Rect(10, 110, 200, 50), "brad: " + bigRadius);
        GUI.Label(new Rect(10, 130, 200, 50), "num: " + boidsPerGroup);
        GUI.Label(new Rect(10, 150, 200, 50), "fps: " + (1f/Time.deltaTime).ToString("F1"));
    }

    private void OnDestroy()
    {
        if(buffer != null)
        {
            buffer.Release();
        }
    }

    public List<Boid> CreateBoidsList(int amount)
    {
        List<Boid> boids = new List<Boid>();
        for (int i = 0; i < amount; i++)
        {
            Boid boid = Instantiate(BoidPrefab, transform.position + Random.insideUnitSphere * 2f, Quaternion.identity);
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

    public void AddBoids(List<Boid> newBoids)
    {
        boids.AddRange(newBoids);
    }
}
