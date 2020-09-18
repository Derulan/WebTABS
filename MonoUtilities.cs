using UnityEngine;

namespace WebTabs
{
    public static class MonoUtilities
    {
        public class SwipeRotate : MonoBehaviour
        {
            private float lastTouchX;

            public float sensitivity = 1f;

            private void Update()
            {
                if(Input.GetMouseButtonDown(0)) lastTouchX = Input.mousePosition.x;
                if(Input.GetMouseButton(0))
                {
                    transform.RotateAround(transform.position, Vector3.up, (Input.mousePosition.x - lastTouchX) * sensitivity);
                    lastTouchX = Input.mousePosition.x;
                }
            }
        }

        public class LerpPosition : MonoBehaviour
        {
            public float followSpeed = 1f;
            public Transform follow;

            private void Update()
            {
                Vector3 followPos = (follow ? follow.position : Vector3.zero);
                transform.position = Vector3.Lerp(transform.position, followPos, followSpeed*Time.deltaTime);
            }
        }

        public class LookAtTransform : MonoBehaviour
        {
            public float followSpeed = 1f;

            public Transform follow;

            private void Update()
            {
                Vector3 followPos = (follow ? follow.position : Vector3.zero);
                Quaternion target = Quaternion.LookRotation(followPos - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, followSpeed * Time.deltaTime);
            }
        }

        public class CameraDolly : MonoBehaviour
        {
            public float sensitivity = 1f;

            public float updateSpeed = 1f;

            public Transform follow;

            public Vector2 bounds = new Vector2(0, 1);

            private float scrolled;


            private void Start()
            {
                Vector3 followPositon = (follow ? follow.position : Vector3.zero);
                Vector3 relativePosition = transform.position - followPositon;
                scrolled = relativePosition.magnitude;
                scrolled = Mathf.Clamp(scrolled, bounds.x, bounds.y);
            }
            private void Update()
            {
                scrolled += sensitivity * -Input.mouseScrollDelta.y * Time.deltaTime;
                scrolled = Mathf.Clamp(scrolled, bounds.x, bounds.y);
                Vector3 followPositon = (follow ? follow.position : Vector3.zero);
                Vector3 relativePosition = transform.position - followPositon;
                Vector3 target = relativePosition.normalized * scrolled + followPositon;
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * updateSpeed);
            }
        }

        public class SimpleEscapeToMenu : MonoBehaviour
        {
            private void Update()
            {
                if(Input.GetKeyDown(KeyCode.Escape)) TABSSceneManager.LoadMainMenu();
            }
        }
    }
}