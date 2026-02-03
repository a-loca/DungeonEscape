using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class RaysHelper : MonoBehaviour
{
    private RayPerceptionSensorComponent3D rays;

    void Start()
    {
        rays = GetComponent<RayPerceptionSensorComponent3D>();
    }

    public bool CanSeeDragon()
    {
        var rayOutputs = RayPerceptionSensor.Perceive(rays.GetRayPerceptionInput()).RayOutputs;
        int lengthOfRayOutputs = rayOutputs.Length;

        // Alternating Ray Order: it gives an order of
        // (0, -delta, delta, -2delta, 2delta, ..., -ndelta, ndelta)
        // index 0 indicates the center of raycasts
        for (int i = 0; i < lengthOfRayOutputs; i++)
        {
            GameObject goHit = rayOutputs[i].HitGameObject;
            if (goHit != null)
            {
                if (goHit.tag == "Dragon")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
