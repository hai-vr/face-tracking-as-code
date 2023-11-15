#if UNITY_EDITOR
using System;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.NDMFProcessor;
using FaceTraAnimator.Runtime;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

[assembly: ExportsPlugin(typeof(FTAPlugin))]
namespace FaceTraAnimator.Runtime
{
    public class FaceTraAnimator : MonoBehaviour, IEditorOnly
    {
        public SkinnedMeshRenderer body;
    }

    public class FTAPlugin : AacPlugin<FaceTraAnimator>
    {
        protected override AacPluginOutput Execute()
        {
            // var fx = CreateFXLayers();
            // CreateAdditiveLayer();

            var fxi = CreateFXInverse();

            var maAc = MaAc.Create(my.gameObject);
            maAc.NewMergeAnimator(fxi, VRCAvatarDescriptor.AnimLayerType.FX);

            return AacPluginOutput.Regular();
        }

        private AacFlController CreateFXInverse()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();

            var interpolationAmount = fx.FloatParameter("AMOUNT");
            fx.OverrideValue(interpolationAmount, 0.8f);

            var smrs = new[] { my.body };
            var tree = MegaTree.NewMegaTree(aac, fx, fx.FloatParameter("ONE"))
                .ShapeActuator(smrs, "HaiXT_EyeClosedInverse_Smile*",
                    Actuator.Inverse("OSCm/Proxy/FT/v2/EyeLid*"),
                    Actuator.Custom("OSCm/Proxy/FT/v2/SmileFrown*", 0.1f, 0.6f)
                )
                .Tree;

            fx.NewState("Direct").WithWriteDefaultsSetTo(true).WithAnimation(tree);

            return ctrl;
        }

        private AacFlController CreateFXLayers()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();

            var interpolationAmount = fx.FloatParameter("AMOUNT");
            fx.OverrideValue(interpolationAmount, 0.8f);

            SkinnedMeshRenderer[] meshes = { };
            var tree = MegaTree.NewMegaTree(aac, fx, fx.FloatParameter("ONE"))
                .BinaryDecoder(fx.FloatParameter("DECODED"), "Prefix_", 4)
                .Interpolator(fx.FloatParameter("SMOOTHED"), fx.FloatParameter("DECODED"), interpolationAmount, MegaTree.InterpolatorRange.Joystick)
                // .ShapeActuator(fx.FloatParameter("ACTUATOR"), meshes, ActuatorMode.Regular, "bsn")
                .Tree;

            fx.NewState("Direct").WithAnimation(tree);

            return ctrl;
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

    internal enum ActuatorMode
    {
        Regular, Inverse, Custom
    }

    internal class MegaTree
    {
        private const string Left = "Left";
        private const string Right = "Right";
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

        public MegaTree ShapeActuator(SkinnedMeshRenderer[] smrs, string shapeName, Actuator actuator)
        {
            return ShapeActuator(smrs, shapeName, new[] { actuator });
        }

        public MegaTree ShapeActuator(SkinnedMeshRenderer[] smrs, string shapeName, params Actuator[] actuators)
        {
            if (shapeName.Contains("*"))
            {
                ForLeftRight(side =>
                {
                    ShapeActuator(smrs, shapeName.Replace("*", side), actuators
                        .Select(actuator =>
                        {
                            actuator.param = actuator.param.Replace("*", side);
                            return actuator;
                        })
                        .ToArray());
                });
                return this;
            }
            
            var tree = ShapeActuatorComplex(actuators, smrs, shapeName);
            _direct.WithAnimation(tree, _one);
            return this;
        }

        private void ForLeftRight(Action<string> executor)
        {
            foreach (var side in new[] { Left, Right })
            {
                executor(side);
            }
        }

        private AacFlBlendTree1D ShapeActuatorComplex(Actuator[] actuators, SkinnedMeshRenderer[] smrs, string shapeName)
        {
            var inactive = BlendShape(smrs, shapeName, 0f);
            var active = BlendShape(smrs, shapeName, 100f);

            AacFlBlendTree1D previousTree = null;
            for (var index = actuators.Length - 1; index >= 0; index--)
            {
                var actuator = actuators[index];
                
                var activation = previousTree != null ? (Motion)previousTree.BlendTree : active.Clip;
                
                var tree = aac.NewBlendTree().Simple1D(_layer.FloatParameter(actuator.param));
                var mode = actuator.mode;
                switch (mode)
                {
                    case ActuatorMode.Regular:
                        tree.WithAnimation(inactive, 0f);
                        tree.WithAnimation(activation, 1f);
                        break;
                    case ActuatorMode.Inverse:
                        tree.WithAnimation(activation, 0f);
                        tree.WithAnimation(inactive, 1f);
                        break;
                    case ActuatorMode.Custom:
                        tree.WithAnimation(inactive, actuator.inactiveValue);
                        tree.WithAnimation(activation, actuator.activeValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }

                previousTree = tree;
            }

            return previousTree;
        }

        private AacFlClip AAP(AacFlFloatParameter output, float value)
        {
            return aac.NewClip().Animating(clip => clip.AnimatesAnimator(output).WithOneFrame(value));
        }

        private AacFlClip BlendShape(SkinnedMeshRenderer[] smrs, string shapeName, float rawValue)
        {
            return aac.NewClip().Animating(clip => clip.Animates(smrs, $"blendShape.{shapeName}").WithOneFrame(rawValue));
        }
    }

    internal struct Actuator
    {
        public string param;
        public ActuatorMode mode;
        public float inactiveValue;
        public float activeValue;

        public static Actuator Regular(string param)
        {
            return new Actuator
            {
                param = param,
                mode = ActuatorMode.Regular
            };
        }

        public static Actuator Inverse(string param)
        {
            return new Actuator
            {
                param = param,
                mode = ActuatorMode.Inverse
            };
        }

        public static Actuator NewActuator(string param, ActuatorMode mode)
        {
            return new Actuator
            {
                param = param,
                mode = mode
            };
        }

        public static Actuator Custom(string param, float inactiveValue, float activeValue)
        {
            return new Actuator
            {
                param = param,
                mode = ActuatorMode.Custom,
                inactiveValue = inactiveValue,
                activeValue = activeValue
            };
        }
    }
}
#endif