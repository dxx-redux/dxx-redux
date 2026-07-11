using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Drives the ShipSim from player input (legacy Input for now) and mirrors
    /// the sim state onto this transform. Mouse pitch/heading is always on;
    /// W/S forward, A/D slide, Space/LeftCtrl vertical, Q/E bank, Shift = full
    /// key thrust is implicit (keys give the whole frame's time, like holding
    /// a key did in DOS Descent).
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        public float mouseSensitivity = 0.4f;

        ShipSim sim;
        ShipState state;
        ShipParams shipParams;
        double gameTime;

        public ShipState State => state;

        public void Init(SegmentWorld world, ShipParams p, System.Numerics.Vector3 pos, Mat3 orient, int segnum)
        {
            sim = new ShipSim(world);
            shipParams = p;
            state = new ShipState { Pos = pos, Orient = orient, Segnum = segnum };
            SyncTransform();
        }

        void Update()
        {
            if (sim == null)
                return;

            float ft = Mathf.Min(Time.deltaTime, 0.25f);
            if (ft <= 0f)
                return;

            var c = new ShipControls();
            if (Input.GetKey(KeyCode.W)) c.ForwardTime += ft;
            if (Input.GetKey(KeyCode.S)) c.ForwardTime -= ft;
            if (Input.GetKey(KeyCode.D)) c.SidewaysTime += ft;
            if (Input.GetKey(KeyCode.A)) c.SidewaysTime -= ft;
            if (Input.GetKey(KeyCode.Space)) c.VerticalTime += ft;
            if (Input.GetKey(KeyCode.LeftControl)) c.VerticalTime -= ft;
            if (Input.GetKey(KeyCode.Q)) c.BankTime += ft;
            if (Input.GetKey(KeyCode.E)) c.BankTime -= ft;

            c.HeadingTime += Input.GetAxis("Mouse X") * mouseSensitivity * ft;
            c.PitchTime -= Input.GetAxis("Mouse Y") * mouseSensitivity * ft;

            c.PitchTime = Mathf.Clamp(c.PitchTime, -ft, ft);
            c.HeadingTime = Mathf.Clamp(c.HeadingTime, -ft, ft);
            c.BankTime = Mathf.Clamp(c.BankTime, -ft, ft);

            gameTime += ft;
            sim.Step(state, shipParams, c, ft, gameTime);
            SyncTransform();
        }

        void SyncTransform()
        {
            transform.position = new Vector3(state.Pos.X, state.Pos.Y, state.Pos.Z);
            transform.rotation = Quaternion.LookRotation(
                new Vector3(state.Orient.Forward.X, state.Orient.Forward.Y, state.Orient.Forward.Z),
                new Vector3(state.Orient.Up.X, state.Orient.Up.Y, state.Orient.Up.Z));
        }
    }
}
