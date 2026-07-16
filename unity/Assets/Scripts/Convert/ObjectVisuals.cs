using LibDescent.Data;

namespace D1U.Convert
{
    public enum ObjectVisualKind { None, Model, Sprite }

    public struct ObjectVisual
    {
        public ObjectVisualKind Kind;
        public int ModelNum;   // into pig Models / BaseDxu.Models
        public int VClipNum;   // into pig VClips
    }

    /// <summary>
    /// Resolves what a placed level object looks like, mirroring
    /// gamesave.c verify_object: robots take their model from robot_info,
    /// powerups their vclip from powerup_info, the reactor its model from
    /// the object-type table; the rest use what the level stored.
    /// </summary>
    public static class ObjectVisuals
    {
        public static ObjectVisual Resolve(Descent1PIGFile pig, ObjectRecord obj)
        {
            switch (obj.Type)
            {
                case 2: // robot
                    if (obj.SubtypeId < pig.numRobots)
                        return Model(pig.Robots[obj.SubtypeId].ModelNum);
                    return None();

                case 9: // reactor
                    if (obj.ModelNum >= 0 && obj.ModelNum < pig.numModels)
                        return Model(obj.ModelNum);
                    return Model(FindReactorModel(pig));

                case 7: // powerup
                    if (obj.SubtypeId < pig.Powerups.Length)
                        return Sprite(pig.Powerups[obj.SubtypeId].VClipNum);
                    return None();

                case 3: // hostage
                    return Sprite(obj.VClipNum);

                case 5:  // placed weapon (mines)
                case 11: // clutter
                    return obj.ModelNum >= 0 && obj.ModelNum < pig.numModels
                        ? Model(obj.ModelNum) : None();

                default: // player/coop starts, ghosts, lights...
                    return None();
            }
        }

        static int FindReactorModel(Descent1PIGFile pig)
        {
            for (int i = 0; i < pig.ObjectTypes.Length; i++)
                if (pig.ObjectTypes[i].type == EditorObjectType.ControlCenter)
                    return pig.ObjectTypes[i].id;
            return -1;
        }

        static ObjectVisual Model(int modelNum) => modelNum >= 0
            ? new ObjectVisual { Kind = ObjectVisualKind.Model, ModelNum = modelNum, VClipNum = -1 }
            : None();

        static ObjectVisual Sprite(int vclipNum) => vclipNum >= 0
            ? new ObjectVisual { Kind = ObjectVisualKind.Sprite, ModelNum = -1, VClipNum = vclipNum }
            : None();

        static ObjectVisual None() => new ObjectVisual { Kind = ObjectVisualKind.None, ModelNum = -1, VClipNum = -1 };
    }
}
