using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    public GameObject[] art;
    public Vector3 direction;
    public Vector3 desiredDirection;
    public float rotationSpeed = 360f;
    public float moveSpeed = 1;
    public float radius = 5f;

    private Vector3 acceleration;
    private Vector3 velocity;

    public BoidData data = new BoidData();
    public struct BoidData
    {
        public Vector3 pos;
        public Vector3 desiredDir;
        public Vector3 curDir;
    };
    // Start is called before the first frame update
    void Start()
    {
        int rnd = Random.Range(0, art.Length);
        art[rnd].SetActive(true);
        data = new BoidData();
        data.pos = transform.position;
        data.curDir = transform.forward;
        data.desiredDir = Random.insideUnitSphere;
    }

    /// <summary>
    /// This function uses the data from the Compute Shader
    /// </summary>
    /// <param name="data"></param>
    public void UpdateData(BoidData data)
    {
        acceleration += data.desiredDir / rotationSpeed;

        velocity += acceleration;
        acceleration = Vector3.zero;

        if (data.desiredDir.sqrMagnitude > Mathf.Epsilon)
        {
            transform.rotation = Quaternion.LookRotation(velocity);// Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(data.desiredDir), rotationSpeed * Time.deltaTime);
        }
        velocity = Vector3.ClampMagnitude(velocity, moveSpeed);
        transform.position += velocity * Time.deltaTime;
        this.data.pos = transform.position;
        this.data.curDir = transform.forward;
    }

    //Old stuff below for a naive implementation
    public void UpdateBoid(List<Boid> boids, List<float> weights, Bounds bounds)
    {
        Vector3 coh = Cohesion(boids) * weights[0];
        Vector3 sep = Separation(boids, radius) * weights[1];
        Vector3 align = Alignment(boids, radius) * weights[2];
        Vector3 bou = ApplyBounds(bounds) * weights[3];
        desiredDirection = (coh + sep + align + bou) / 4f;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desiredDirection), rotationSpeed * Time.deltaTime);
        direction = transform.forward;
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    public Vector3 ApplyBounds(Bounds bounds)
    {
        if (!bounds.Contains(transform.position))
        {
            return bounds.center - transform.position;
        }
        return Vector3.zero;
    }

    public Vector3 Cohesion(List<Boid> boids)
    {
        Vector3 result = Vector3.zero;
        foreach(Boid boid in boids)
        {
            if(boid == this) { continue; }
            result += boid.transform.position;
        }
        result /= (boids.Count - 1);
        return result - transform.position;
    }

    public Vector3 Separation(List<Boid> boids, float radius)
    {
        Vector3 result = Vector3.zero;
        int count = 0;
        foreach (Boid boid in boids)
        {
            if (Vector3.Distance(transform.position, boid.transform.position) > radius) { continue; }
            result -= (boid.transform.position - transform.position);
            count++;
        }
        result /= count;
        return result;
    }

    public Vector3 Alignment(List<Boid> boids, float radius)
    {
        Vector3 result = Vector3.zero;
        int count = 0;
        foreach (Boid boid in boids)
        {
            if (Vector3.Distance(transform.position, boid.transform.position) > radius) { continue; }
            result += boid.direction;
            count++;
        }
        result /= count;
        return result;
    }
}
