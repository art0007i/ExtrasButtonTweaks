using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System.Reflection.Emit;
using FrooxEngine.UIX;
using Elements.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ExtrasButtonTweaks;

public class ExtrasButtonTweaks : ResoniteMod
{
    public override string Name => "ExtrasButtonTweaks";
    public override string Author => "art0007i";
    public override string Version => "1.0.0";
    public override string Link => "https://github.com/art0007i/ExtrasButtonTweaks/";

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_BUTTON_VISUALS = new("button_visuals", "When true button visuals will be changed.", () => true);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_VECTOR_AVERAGE = new("vector_actions_average", "When true vector average option will be hidden.", () => false);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_VECTOR_SET_TO = new("vector_actions_set_to", "When true vector set to options will be hidden.", () => false);

    public static ModConfiguration config;

    public override void OnEngineInit()
    {
        config = GetConfiguration();
        Harmony harmony = new Harmony("me.art0007i.ExtrasButtonTweaks");
        //harmony.PatchAll();

        var displayClass = typeof(InspectorMemberActions).GetNestedType("<>c__DisplayClass1_0", AccessTools.all)?
            .GetNestedType("<<Pressed>b__0>d", AccessTools.all)?
            .GetMethod(nameof(IAsyncStateMachine.MoveNext), AccessTools.all);
        if(displayClass != null)
        {
            harmony.Patch(displayClass, transpiler: new(typeof(RemoveVectorActionsPatch).GetMethod(nameof(RemoveVectorActionsPatch.Transpiler))));
        }
        else
        {
            Warn("Failed to patch context menu, options to disable 'Vector Average' and 'Vector Set To' will not work!");
        }

        var toPatch = AccessTools.Method(typeof(SyncMemberEditorBuilder), "GenerateMemberField");
        var patchFunc = AccessTools.Method(typeof(ExtrasButtonTweaksPatch), nameof(ExtrasButtonTweaksPatch.Transpiler));

        ApplyPatch(harmony, config.GetValue(KEY_BUTTON_VISUALS), toPatch, patchFunc);

        KEY_BUTTON_VISUALS.OnChanged += (newValue) =>
        {
            if (newValue is bool b)
            {
                ApplyPatch(harmony, b, toPatch, patchFunc);
            }
        };
    }

    void ApplyPatch(Harmony harmony, bool b, MethodInfo toPatch, MethodInfo patchFunc)
    {
        if (b) harmony.Patch(toPatch, transpiler: new(patchFunc));
        else harmony.Unpatch(toPatch, patchFunc);
    }

    class ExtrasButtonTweaksPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var lookFor = AccessTools.Method(typeof(DatatypeColorHelper), nameof(DatatypeColorHelper.GetTypeColor));
            var lookForEnd = AccessTools.Method(typeof(DatatypeColorHelper), nameof(DatatypeColorHelper.GetTypeColor));
            bool purge = false;
            foreach (var code in codes)
            {
                if (!purge)
                {
                    yield return code;
                }
                if (code.operand is MethodInfo mf && mf.Name == "Button")
                {
                    purge = false;
                }
                if (code.Calls(lookFor))
                {
                    purge = true;
                    yield return new(OpCodes.Ldarg_2);
                    yield return new(OpCodes.Call, typeof(ExtrasButtonTweaksPatch).GetMethod(nameof(CreateExtrasButton)));
                }
            }
        }

        public static Button CreateExtrasButton(colorX color, UIBuilder ui)
        {
            color = color.MulRGB(0.75f);
            ui.PushStyle();
            ui.Style.ButtonSprite = RadiantUI_Constants.GetOutlinedSprite(ui.World);
            ui.Style.ButtonColor = color;
            var bt = ui.Button("☰", color);
            ui.PopStyle();
            return bt;
        }
    }

    class RemoveVectorActionsPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var lookFor = AccessTools.Method(typeof(ContextMenu), nameof(ContextMenu.AddItem), new Type[] {
                typeof(LocaleString).MakeByRefType(),
                typeof(Uri), 
                typeof(colorX?).MakeByRefType(), 
                typeof(ButtonEventHandler)
            });
            var lookFor2 = AccessTools.Method(typeof(ReflectionExtensions), nameof(ReflectionExtensions.GetVectorDimensions));
            foreach (var code in codes)
            {
                if (code.Calls(lookFor))
                {
                    yield return new(OpCodes.Call, typeof(RemoveVectorActionsPatch).GetMethod(nameof(AddItemProxy)));
                    continue;
                }
                if (code.Calls(lookFor2))
                {
                    yield return new(OpCodes.Call, typeof(RemoveVectorActionsPatch).GetMethod(nameof(VectorDimensionsProxy)));
                    continue;
                }
                yield return code;
            }
        }

        public static MethodInfo averageFunc = AccessTools.Method(typeof(InspectorMemberActions), "SetAllToAvg");

        public static ContextMenuItem AddItemProxy(ContextMenu menu, in LocaleString label, Uri icon, in colorX? color, ButtonEventHandler action)
        {
            if (config.GetValue(KEY_VECTOR_AVERAGE) && action.Method == averageFunc)
            {
                return null;
            }
            return menu.AddItem(in label, icon, in color, action);
        }

        public static int VectorDimensionsProxy(Type type)
        {
            if (config.GetValue(KEY_VECTOR_SET_TO)) return 0;
            else return type.GetVectorDimensions();
        }
    }
}
