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

    public (bool, float, float) CanSeeObjectWithTag(string tag)
    {
        var rayOutputs = RayPerceptionSensor.Perceive(rays.GetRayPerceptionInput()).RayOutputs;
        int lengthOfRayOutputs = rayOutputs.Length;

        bool objectVisible = false;
        float angle = 0;
        float closestDistance = float.MaxValue;

        // Alternating Ray Order: it gives an order of
        // (0, -delta, delta, -2delta, 2delta, ..., -ndelta, ndelta)
        // index 0 indicates the center of raycasts
        for (int i = 0; i < lengthOfRayOutputs; i++)
        {
            GameObject goHit = rayOutputs[i].HitGameObject;
            if (goHit != null && goHit.CompareTag(tag))
            {
                objectVisible = true;

                // Calc distance
                var rayDirection =
                    rayOutputs[i].EndPositionWorld - rayOutputs[i].StartPositionWorld;
                var scaledRayLength = rayDirection.magnitude;
                float hitDistance = rayOutputs[i].HitFraction * scaledRayLength;

                if (hitDistance < closestDistance)
                {
                    objectVisible = true;
                    closestDistance = hitDistance;

                    // Calc angle between agent and dragon
                    var rayDirectionNormalized = rayDirection.normalized;
                    var forward = transform.forward;
                    angle = Vector3.SignedAngle(forward, rayDirectionNormalized, Vector3.up);
                }

            }
        }

        if (!objectVisible)
        {
            return (false, 0f, 0f);
        }

        return (objectVisible, closestDistance, angle);
    }
}
