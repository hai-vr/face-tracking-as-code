#if UNITY_EDITOR
using System;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.NDMFProcessor;
using FaceTraAnimator.Runtime;
using nadena.dev.ndmf;
using UnityEngine;

[assembly: ExportsPlugin(typeof(FTAPlugin))]
namespace FaceTraAnimator.Runtime
{
    public class FaceTraAnimator : MonoBehaviour
    {
    }

    public class FTAPlugin : AacPlugin<FaceTraAnimator>
    {
        protected override AacPluginOutput Execute()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();

            BinaryDecoder(fx.FloatParameter("OUTPUT"), fx, "Prefix_", 4);
            
            return AacPluginOutput.Regular();
        }

        private AacFlBlendTreeDirect BinaryDecoder(AacFlFloatParameter output, AacFlLayer layer, string prefix, int totalBits)
        {
            var lowToHigh = Enumerable.Range(0, totalBits)
                .Select(index => $"{prefix}_{BitValueContribution(index)}")
                .Select(layer.FloatParameter)
                .ToArray();

            return BinaryDecoder(output, lowToHigh);
        }

        private AacFlBlendTreeDirect BinaryDecoder(AacFlFloatParameter output, params AacFlFloatParameter[] lowToHigh)
        {
            var result = aac.NewBlendTree().Direct();
            for (var index = 0; index < lowToHigh.Length; index++)
            {
                result.WithAnimation(AAP(output, BitValueContribution(index)), lowToHigh[index]);
            }
            
            return result;
        }

        private static int BitValueContribution(int index)
        {
            return (int)Math.Pow(2, index);
        }

        private AacFlClip AAP(AacFlFloatParameter output, float value)
        {
            return aac.NewClip().Animating(clip => clip.AnimatesAnimator(output).WithOneFrame(value));
        }
    }
}
#endif