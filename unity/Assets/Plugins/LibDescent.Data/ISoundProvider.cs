using System.Collections.Generic;

namespace LibDescent.Data
{
    public interface ISoundProvider
    {
        List<SoundData> Sounds { get; }
    }
}
