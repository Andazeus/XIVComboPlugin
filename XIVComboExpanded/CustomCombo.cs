﻿using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Utility;
using XIVComboExpandedPlugin.Attributes;

namespace XIVComboExpandedPlugin.Combos
{
    /// <summary>
    /// Base class for each combo.
    /// </summary>
    internal abstract partial class CustomCombo
    {
        private const uint InvalidObjectID = 0xE000_0000;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomCombo"/> class.
        /// </summary>
        protected CustomCombo()
        {
            var presetInfo = this.Preset.GetAttribute<CustomComboInfoAttribute>();
            this.JobID = presetInfo.JobID;
            this.ClassID = this.JobID switch
            {
                ADV.JobID => ADV.ClassID,
                BLM.JobID => BLM.ClassID,
                BRD.JobID => BRD.ClassID,
                DRG.JobID => DRG.ClassID,
                MNK.JobID => MNK.ClassID,
                NIN.JobID => NIN.ClassID,
                PLD.JobID => PLD.ClassID,
                SCH.JobID => SCH.ClassID,
                SMN.JobID => SMN.ClassID,
                WAR.JobID => WAR.ClassID,
                WHM.JobID => WHM.ClassID,
                _ => 0xFF,
            };
            this.ActionIDs = presetInfo.ActionIDs;
        }

        /// <summary>
        /// Gets the preset associated with this combo.
        /// </summary>
        protected abstract CustomComboPreset Preset { get; }

        /// <summary>
        /// Gets the class ID associated with this combo.
        /// </summary>
        protected byte ClassID { get; }

        /// <summary>
        /// Gets the job ID associated with this combo.
        /// </summary>
        protected byte JobID { get; }

        /// <summary>
        /// Gets the action IDs associated with this combo.
        /// </summary>
        protected virtual uint[] ActionIDs { get; }

        /// <summary>
        /// Performs various checks then attempts to invoke the combo.
        /// </summary>
        /// <param name="actionID">Starting action ID.</param>
        /// <param name="lastComboActionID">Last combo action.</param>
        /// <param name="comboTime">Current combo time.</param>
        /// <param name="level">Current player level.</param>
        /// <param name="newActionID">Replacement action ID.</param>
        /// <returns>True if the action has changed, otherwise false.</returns>
        public bool TryInvoke(uint actionID, uint lastComboActionID, float comboTime, byte level, out uint newActionID)
        {
            newActionID = 0;

            if (!IsEnabled(this.Preset))
                return false;

            var classJobID = LocalPlayer?.ClassJob.Id;
            if (this.JobID != ADV.JobID && this.ClassID != ADV.ClassID &&
                this.JobID != classJobID && this.ClassID != classJobID)
                return false;

            if (!this.ActionIDs.Contains(actionID))
                return false;

            var resultingActionID = this.Invoke(actionID, lastComboActionID, comboTime, level);
            if (resultingActionID == 0 || actionID == resultingActionID)
                return false;

            newActionID = resultingActionID;
            return true;
        }

        /// <summary>
        /// Calculate the best action to use, based on cooldown remaining.
        /// </summary>
        /// <param name="actions">Action data.</param>
        /// <returns>The appropriate action to use.</returns>
        protected static (uint ActionID, IconReplacer.CooldownData Data) CalcBestAction(params (uint ActionID, IconReplacer.CooldownData Data)[] actions)
        {
            static (uint ActionID, IconReplacer.CooldownData Data) Compare(
                (uint ActionID, IconReplacer.CooldownData Data) a1,
                (uint ActionID, IconReplacer.CooldownData Data) a2)
            {
                // Neither, return the first parameter
                if (!a1.Data.IsCooldown && !a2.Data.IsCooldown)
                    return a1;

                // Both, return soonest available
                if (a1.Data.IsCooldown && a2.Data.IsCooldown)
                    return a1.Data.CooldownRemaining < a2.Data.CooldownRemaining ? a1 : a2;

                // One or the other
                return a1.Data.IsCooldown ? a2 : a1;
            }

            return actions.Aggregate((a1, a2) => Compare(a1, a2));
        }

        /// <summary>
        /// Invokes the combo.
        /// </summary>
        /// <param name="actionID">Starting action ID.</param>
        /// <param name="lastComboActionID">Last combo action.</param>
        /// <param name="comboTime">Current combo time.</param>
        /// <param name="level">Current player level.</param>
        /// <returns>The replacement action ID.</returns>
        protected abstract uint Invoke(uint actionID, uint lastComboActionID, float comboTime, byte level);
    }

    /// <summary>
    /// Passthrough methods and properties to IconReplacer. Shortens what it takes to call each method.
    /// </summary>
    internal abstract partial class CustomCombo
    {
        private static readonly Dictionary<Type, JobGaugeBase> JobGaugeCache = new();

        /// <summary>
        /// Gets the player or null.
        /// </summary>
        protected static PlayerCharacter? LocalPlayer => Service.ClientState.LocalPlayer;

        /// <summary>
        /// Gets the current target or null.
        /// </summary>
        protected static GameObject? CurrentTarget => Service.TargetManager.Target;

        /// <summary>
        /// Calls the original hook.
        /// </summary>
        /// <param name="actionID">Action ID.</param>
        /// <returns>The result from the hook.</returns>
        protected static uint OriginalHook(uint actionID) => Service.IconReplacer.OriginalHook(actionID);

        /// <summary>
        /// Determine if the given preset is enabled.
        /// </summary>
        /// <param name="preset">Preset to check.</param>
        /// <returns>A value indicating whether the preset is enabled.</returns>
        protected static bool IsEnabled(CustomComboPreset preset) => Service.Configuration.IsEnabled(preset);

        /// <summary>
        /// Find if the player is in condition.
        /// </summary>
        /// <param name="flag">Condition flag.</param>
        /// <returns>A value indicating whether the player is in the condition.</returns>
        protected static bool HasCondition(ConditionFlag flag) => Service.Condition[flag];

        /// <summary>
        /// Find if an effect on the player exists.
        /// The effect may be owned by the player or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>A value indicating if the effect exists.</returns>
        protected static bool HasEffect(ushort effectID) => FindEffect(effectID) is not null;

        /// <summary>
        /// Finds an effect on the player.
        /// The effect must be owned by the player or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>Status object or null.</returns>
        protected static Status? FindEffect(ushort effectID) => FindEffect(effectID, LocalPlayer, LocalPlayer?.ObjectId);

        /// <summary>
        /// Find if an effect on the target exists.
        /// The effect must be owned by the player or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>A value indicating if the effect exists.</returns>
        protected static bool TargetHasEffect(ushort effectID) => FindTargetEffect(effectID) is not null;

        /// <summary>
        /// Finds an effect on the current target.
        /// The effect must be owned by the player or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>Status object or null.</returns>
        protected static Status? FindTargetEffect(ushort effectID) => FindEffect(effectID, CurrentTarget, LocalPlayer?.ObjectId);

        /// <summary>
        /// Find if an effect on the player exists.
        /// The effect may be owned by anyone or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>A value indicating if the effect exists.</returns>
        protected static bool HasEffectAny(ushort effectID) => FindEffectAny(effectID) is not null;

        /// <summary>
        /// Finds an effect on the player.
        /// The effect may be owned by anyone or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>Status object or null.</returns>
        protected static Status? FindEffectAny(ushort effectID) => FindEffect(effectID, LocalPlayer, null);

        /// <summary>
        /// Find if an effect on the target exists.
        /// The effect may be owned by anyone or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>A value indicating if the effect exists.</returns>
        protected static bool TargetHasEffectAny(ushort effectID) => FindTargetEffectAny(effectID) is not null;

        /// <summary>
        /// Finds an effect on the current target.
        /// The effect may be owned by anyone or unowned.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <returns>Status object or null.</returns>
        protected static Status? FindTargetEffectAny(ushort effectID) => FindEffect(effectID, CurrentTarget, null);

        /// <summary>
        /// Finds an effect on the given object.
        /// </summary>
        /// <param name="effectID">Status effect ID.</param>
        /// <param name="obj">Object to look for effects on.</param>
        /// <param name="sourceID">Source object ID.</param>
        /// <returns>Status object or null.</returns>
        protected static Status? FindEffect(ushort effectID, GameObject? obj, uint? sourceID)
        {
            if (obj is null)
                return null;

            if (obj is not BattleChara chara)
                return null;

            foreach (var status in chara.StatusList)
            {
                if (status.StatusId == effectID && (!sourceID.HasValue || status.SourceID == 0 || status.SourceID == InvalidObjectID || status.SourceID == sourceID))
                    return status;
            }

            return null;
        }

        /// <summary>
        /// Gets the cooldown data for an action.
        /// </summary>
        /// <param name="actionID">Action ID to check.</param>
        /// <returns>Cooldown data.</returns>
        protected static IconReplacer.CooldownData GetCooldown(uint actionID) => Service.IconReplacer.GetCooldown(actionID);

        /// <summary>
        /// Gets the job gauge.
        /// </summary>
        /// <typeparam name="T">Type of job gauge.</typeparam>
        /// <returns>The job gauge.</returns>
        protected static T GetJobGauge<T>() where T : JobGaugeBase
        {
            if (!JobGaugeCache.TryGetValue(typeof(T), out var gauge))
                gauge = JobGaugeCache[typeof(T)] = Service.JobGauges.Get<T>();

            return (T)gauge;
        }
    }
}