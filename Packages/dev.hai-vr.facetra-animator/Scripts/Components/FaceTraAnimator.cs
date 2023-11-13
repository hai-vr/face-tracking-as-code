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
            CreateFXLayers();
            CreateAdditiveLayer();

            return AacPluginOutput.Regular();
        }

        private void CreateFXLayers()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();

            var interpolationAmount = fx.FloatParameter("AMOUNT");
            fx.OverrideValue(interpolationAmount, 0.8f);

            var tree = MegaTree.NewMegaTree(aac, fx, fx.FloatParameter("ONE"))
                .BinaryDecoder(fx.FloatParameter("DECODED"), "Prefix_", 4)
                .Interpolator(fx.FloatParameter("SMOOTHED"), fx.FloatParameter("DECODED"), interpolationAmount, MegaTree.InterpolatorRange.Joystick)
                .Tree;

            fx.NewState("Direct").WithAnimation(tree);
        }

        private void CreateAdditiveLayer()
        {
            var ctrl = aac.NewAnimatorController();
            var ad = ctrl.NewLayer();

            var tree = MegaTree.NewMegaTree(aac, ad, ad.FloatParameter("ONE"))
                .Tree;

            ad.NewState("Direct").WithAnimation(tree);
        }
    }

    internal class MegaTree
    {
        private readonly AacFlBase aac;
        private readonly AacFlBlendTreeDirect _direct;
        private readonly AacFlLayer _layer;
        private readonly AacFlFloatParameter _one;

        public AacFlBlendTreeDirect Tree => _direct;

        public static MegaTree NewMegaTree(AacFlBase aac, AacFlLayer layer, AacFlFloatParameter one)
        {
            layer.OverrideValue(one, 1f);
            return new MegaTree(aac, aac.NewBlendTree().Direct(), layer, one);
        }

        private MegaTree(AacFlBase aac, AacFlBlendTreeDirect direct, AacFlLayer layer, AacFlFloatParameter one)
        {
            this.aac = aac;
            _direct = direct;
            _layer = layer;
            _one = one;
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

        public enum InterpolatorRange
        {
            Joystick,
            Analog
        }
        
        public MegaTree Interpolator(AacFlFloatParameter smoothed, AacFlFloatParameter wanted, AacFlFloatParameter amount, InterpolatorRange range)
        {
            var lower = range == InterpolatorRange.Joystick ? -1f : 0f;
            var upper = 1f;
            return Interpolator(smoothed, wanted, amount, lower, upper);
        }

        private MegaTree Interpolator(AacFlFloatParameter smoothed, AacFlFloatParameter wanted, AacFlFloatParameter amount, float lower, float upper)
        {
            var lowerAnim = AAP(smoothed, lower);
            var upperAnim = AAP(smoothed, upper);
            var interpolator = aac.NewBlendTree().Simple1D(amount)
                .WithAnimation(aac.NewBlendTree().Simple1D(wanted)
                        .WithAnimation(lowerAnim, lower)
                        .WithAnimation(upperAnim, upper),
                    0)
                .WithAnimation(aac.NewBlendTree().Simple1D(smoothed)
                        .WithAnimation(lowerAnim, lower)
                        .WithAnimation(upperAnim, upper),
                    1);
            _direct.WithAnimation(interpolator, _one);

            return this;
        }

        private AacFlClip AAP(AacFlFloatParameter output, float value)
        {
            return aac.NewClip().Animating(clip => clip.AnimatesAnimator(output).WithOneFrame(value));
        }
    }
}
#endif