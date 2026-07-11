using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Dev fly camera (legacy input): hold right mouse to look, WASD + QE to
    /// move, Shift to boost, scroll wheel to change speed.
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        public float speed = 30f;
        public float lookSensitivity = 2.5f;

        float yaw, pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }

        void Update()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
                speed = Mathf.Clamp(speed * (1f + scroll * 0.1f), 2f, 300f);

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (Input.GetKey(KeyCode.Q)) move += Vector3.down;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;

            float boost = Input.GetKey(KeyCode.LeftShift) ? 3f : 1f;
            transform.position += transform.rotation * move * (speed * boost * Time.deltaTime);
        }
    }
}
