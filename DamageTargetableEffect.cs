using System;
using UnityEngine;

namespace WebTabs
{
    public class DamageTargetableEffect : TargetableEffect
    {
        public override void DoEffect(Transform startPoint, Transform target)
        {
            Vector3 forceVector = (target.position - startPoint.position).normalized * force;
            HealthHandler healthHandler = target.transform.root.GetComponentInChildren<HealthHandler>();
            if(healthHandler) healthHandler.TakeDamage(damage, forceVector);
            RigidbodyHolder rigidbodyHolder = target.transform.root.GetComponentInChildren<RigidbodyHolder>();
            Rigidbody[] rigidbodies = null;
            if(rigidbodyHolder) 
            {
                rigidbodies = rigidbodyHolder.AllRigs;
                foreach(Rigidbody rigidbody in rigidbodies)
                {
                    WilhelmPhysicsFunctions.AddForceWithMinWeight(rigidbody, forceVector, ForceMode.Impulse, 0f);
                }
            }
        }

        public override void DoEffect(Vector3 startPoint, Vector3 endPoint, Rigidbody targetRig = null)
        {
        }
        public float damage = 1f;

        public float force = 1f;
    }
}

