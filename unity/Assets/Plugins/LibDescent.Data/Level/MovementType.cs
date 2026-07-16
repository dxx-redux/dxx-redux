using System;
using System.Collections.Generic;
using System.Text;

namespace LibDescent.Data
{
    public static class MovementTypeFactory
    {
        public static MovementType NewMovementType(MovementTypeID id)
        {
            switch (id)
            {
                case MovementTypeID.Physics:
                    return new PhysicsMoveType();
                case MovementTypeID.Spinning:
                    return new SpinningMoveType();

                case MovementTypeID.None:
                    return new NullMovementType();
            }
            throw new ArgumentException("MovementTypeFactory::NewMovementType: bad movementtype");
        }
    }
    public abstract class MovementType
    {
        public abstract MovementTypeID Identifier { get; }
    }

    [Flags]
    public enum PhysicsFlags
    {
        /// <summary>
        /// roll when turning
        /// </summary>
        Turnroll = 0x01,
        /// <summary>
        /// level object with closest side
        /// </summary>
        Levelling = 0x02,
        /// <summary>
        /// bounce (not slide) when hit wall
        /// </summary>
        Bounce = 0x04,
        /// <summary>
        /// wiggle while flying (players only)
        /// </summary>
        Wiggle = 0x08,
        /// <summary>
        /// object sticks (stops moving) when hits wall
        /// </summary>
        Stick = 0x10,
        /// <summary>
        /// object keeps going even after it hits another object (eg, fusion cannon)
        /// </summary>
        Persistent = 0x20,
        /// <summary>
        /// this object uses its thrust
        /// </summary>
        UsesThrust = 0x40
    }

    public class PhysicsMoveType : MovementType
    {
        public override MovementTypeID Identifier => MovementTypeID.Physics; 

        public FixVector Velocity { get; set; }
        public FixVector Thrust { get; set; }
        public Fix Mass { get; set; }
        public Fix Drag { get; set; }
        public Fix Brakes { get; set; }
        public FixVector AngularVel { get; set; }
        public FixVector RotationalThrust { get; set; }
        public short Turnroll { get; set; } //fixang
        public PhysicsFlags Flags { get; set; }
    }

    public class SpinningMoveType : MovementType
    {
        public override MovementTypeID Identifier => MovementTypeID.Spinning;

        public FixVector SpinRate { get; set; }
    }

    public class NullMovementType : MovementType
    {
        public override MovementTypeID Identifier => MovementTypeID.None;
    }
}
