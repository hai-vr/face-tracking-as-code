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

            var tree = MegaTree.NewMegaTree(aac, fx)
                .BinaryDecoder(fx.FloatParameter("OUTPUT"), "Prefix_", 4)
                .Tree;

            fx.NewState("Direct").WithAnimation(tree);

            return AacPluginOutput.Regular();
        }
    }

    internal class MegaTree
    {
        private readonly AacFlBase aac;
        private readonly AacFlBlendTreeDirect _direct;
        private readonly AacFlLayer _layer;

        public AacFlBlendTreeDirect Tree => _direct;

        public static MegaTree NewMegaTree(AacFlBase aac, AacFlLayer layer)
        {
            return new MegaTree(aac, aac.NewBlendTree().Direct(), layer);
        }

        private MegaTree(AacFlBase aac, AacFlBlendTreeDirect direct, AacFlLayer layer)
        {
            this.aac = aac;
            _direct = direct;
            _layer = layer;
        }

        public MegaTree BinaryDecoder(AacFlFloatParameter output, string prefix, int totalBits)
        {
            var lowToHigh = Enumerable.Range(0, totalBits)
                .Select(index => $"{prefix}_{BitValueContribution(index)}")
                .Select(_layer.FloatParameter)
                .ToArray();

            BinaryDecoder(output, lowToHigh);

            return this;
        }

        private void BinaryDecoder(AacFlFloatParameter output, params AacFlFloatParameter[] lowToHigh)
        {
            for (var index = 0; index < lowToHigh.Length; index++)
            {
                _direct.WithAnimation(AAP(output, BitValueContribution(index)), lowToHigh[index]);
            }
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