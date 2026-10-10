using System.Collections.Generic;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    public sealed class RothPlayerInteractor : MonoBehaviour
    {
        public Camera ViewCamera;
        public RothCommandMonitor Commands;
        public float UseDistance=3.0f;
        public float TouchRadius=0.45f;
        private readonly HashSet<int> _touching=new HashSet<int>();

        private void Awake(){if(ViewCamera==null)ViewCamera=GetComponentInChildren<Camera>();}
        private void Update()
        {
            if(Commands==null) return;
            if(Input.GetMouseButtonDown(0)) TryUse(false);
            if(Input.GetMouseButtonDown(1)) TryUse(true);
            UpdateTouches();
        }
        private void TryUse(bool right)
        {
            if(ViewCamera==null) return;
            Ray ray=new Ray(ViewCamera.transform.position,ViewCamera.transform.forward); RaycastHit hit;
            if(!Physics.Raycast(ray,out hit,UseDistance,~0,QueryTriggerInteraction.Collide)) return;
            RothObjectTag tag=hit.collider.GetComponentInParent<RothObjectTag>();
            if(tag!=null) { if(right) Commands.TriggerObjectRightClick(tag.ObjectId); else Commands.TriggerObjectLeftClick(tag.ObjectId); return; }
            RothMapMeshBuilder map=hit.collider.GetComponentInParent<RothMapMeshBuilder>();
            if(map!=null) Commands.TriggerWorldClick(hit.point,hit.normal,right);
        }
        private void UpdateTouches()
        {
            Collider[] hits=Physics.OverlapSphere(transform.position,TouchRadius,~0,QueryTriggerInteraction.Collide);
            var now=new HashSet<int>();
            for(int i=0;i<hits.Length;i++)
            {
                RothObjectTag tag=hits[i].GetComponentInParent<RothObjectTag>(); if(tag==null) continue;
                int key=tag.ObjectIndex; now.Add(key); if(!_touching.Contains(key)) Commands.TriggerObjectTouch(tag.ObjectId);
            }
            _touching.Clear(); foreach(int key in now)_touching.Add(key);
        }
    }
}
