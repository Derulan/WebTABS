using System;
using UnityEngine;

namespace WebTabs
{
    public class SpawnTargetableEffect : TargetableEffect
    {
        public override void DoEffect(Transform startPoint, Transform target)
        {
            if(objectToSpawn)
            {
                Quaternion rotation = rotation = Quaternion.LookRotation(Vector3.up);
                GameObject.Instantiate<GameObject>(this.objectToSpawn, target.position, rotation);
            }
        }

        public override void DoEffect(Vector3 startPoint, Vector3 endPoint, Rigidbody targetRig = null)
        {
        }
        public GameObject objectToSpawn;
    }
}

