using System;
using Lemegeton.Core;
using System.Collections.Generic;
using System.Linq;

namespace Lemegeton.Content
{
    internal class EwTrialsRubicante : Core.Content
    {
        public override FeaturesEnum Features => FeaturesEnum.None;

        private const int StatusFurious = 0xd9c;
        private const int StatusBlooming = 0xd9b;
        private const int StatusStinging = 0xd9d;

        private bool ZoneOk = false;

        private FlamespireAM _flamespireAM;

        #region FlamespireAM

        public class FlamespireAM : Automarker
        {
            [AttributeOrderNumber(1000)]
            public AutomarkerSigns Signs { get; set; }

            [AttributeOrderNumber(2000)]
            public AutomarkerPrio Prio { get; set; }

            [DebugOption]
            [AttributeOrderNumber(2500)]
            public AutomarkerTiming Timing { get; set; }

            [DebugOption]
            [AttributeOrderNumber(3000)]
            public Action Test { get; set; }

            private uint _share = 0;
            private List<uint> _spreads = new List<uint>();
            private List<uint> _stacks = new List<uint>();
            private bool _fired = false;

            public FlamespireAM(State state) : base(state)
            {
                Enabled = false;
                Signs = new AutomarkerSigns();
                Prio = new AutomarkerPrio();
                Timing = new AutomarkerTiming() { TimingType = AutomarkerTiming.TimingTypeEnum.Inherit, Parent = state.cfg.DefaultAutomarkerTiming };
                Prio.Priority = AutomarkerPrio.PrioTypeEnum.Job;
                Signs.SetRole("ShareTarget", AutomarkerSigns.SignEnum.Circle, false);
                Signs.SetRole("Share1", AutomarkerSigns.SignEnum.Bind1, false);
                Signs.SetRole("Share2", AutomarkerSigns.SignEnum.Bind2, false);
                Signs.SetRole("Share3", AutomarkerSigns.SignEnum.Bind3, false);
                Signs.SetRole("Spread1", AutomarkerSigns.SignEnum.Attack1, false);
                Signs.SetRole("Spread2", AutomarkerSigns.SignEnum.Attack2, false);
                Signs.SetRole("Spread3", AutomarkerSigns.SignEnum.Attack3, false);
                Signs.SetRole("Spread4", AutomarkerSigns.SignEnum.Attack4, false);
                Test = new Action(() => Signs.TestFunctionality(state, Prio, Timing, SelfMarkOnly, AsSoftmarker));
            }
            public override void Reset()
            {
                Log(State.LogLevelEnum.Debug, null, "Reset");
                _spreads.Clear();
                _stacks.Clear();
                _share = 0;
                _fired = false;
            }

            internal void FeedStatus(uint actorId, uint statusId, bool gained)
            {
                if (Active == false)
                {
                    return;
                }
                Log(State.LogLevelEnum.Debug, null, "Registered status {0} {1} on {2:X}", statusId, gained, actorId);

                if (gained == false)
                {
                    if (_fired == true)
                    {
                        Log(State.LogLevelEnum.Debug, null, "Clearing automarkers");
                        Reset();
                        _state.ClearAutoMarkers();
                    }
                    return;
                }

                if (statusId == StatusFurious)
                {
                    _share = actorId;
                }
                if (statusId == StatusBlooming)
                {
                    _stacks.Add(actorId);
                }
                if (statusId == StatusStinging)
                {
                    _spreads.Add(actorId);
                }
                if (_share == 0 || _stacks.Count < 3 || _spreads.Count < 4)
                {
                    return;
                }

                Log(State.LogLevelEnum.Debug, null, "Ready for automarkers");
                AutomarkerPayload ap;
                AutomarkerPrio.PrioArchetypeEnum role;
                Party.PartyMember pm;
                List<Party.PartyMember> _sharesGo;
                List<Party.PartyMember> _spreadsGo;
                Party pty = _state.GetPartyMembers();
                pm = (from ix in pty.Members where ix.ObjectId == _share select ix).First();
                ap = new AutomarkerPayload(_state, SelfMarkOnly, AsSoftmarker);
                role = AutomarkerPrio.JobToArchetype(pm.Job);
                _sharesGo = new List<Party.PartyMember>(
                from ix in pty.Members
                where AutomarkerPrio.JobToArchetype(ix.Job) == role && ix.ObjectId != _share
                select ix);
                _spreadsGo = new List<Party.PartyMember>(
                                from ix in pty.Members
                                where AutomarkerPrio.JobToArchetype(ix.Job) != role
                                select ix);
                Prio.SortByPriority(_sharesGo);
                Prio.SortByPriority(_spreadsGo);
                ap.Assign(Signs.Roles["ShareTarget"], pm.GameObject);
                ap.Assign(Signs.Roles["Share1"], _sharesGo[0].GameObject);
                ap.Assign(Signs.Roles["Share2"], _sharesGo[1].GameObject);
                ap.Assign(Signs.Roles["Share3"], _sharesGo[2].GameObject);
                ap.Assign(Signs.Roles["Spread1"], _spreadsGo[0].GameObject);
                ap.Assign(Signs.Roles["Spread2"], _spreadsGo[1].GameObject);
                ap.Assign(Signs.Roles["Spread3"], _spreadsGo[2].GameObject);
                ap.Assign(Signs.Roles["Spread4"], _spreadsGo[3].GameObject);
                _state.ExecuteAutomarkers(ap, Timing);
                _fired = true;
            }
        }
            #endregion

        public EwTrialsRubicante(State st) : base(st)
        {
            st.OnZoneChange += OnZoneChange;
        }

        protected override bool ExecutionImplementation()
        {
            if (ZoneOk == true)
            {
                return base.ExecutionImplementation();
            }
            return false;
        }

        private void SubscribeToEvents()
        {
            _state.OnStatusChange += OnStatusChange;
        }

        private void OnStatusChange(uint src, uint dest, uint statusId, bool gained, float duration, int stacks)
        {
            if (statusId == StatusFurious || statusId == StatusBlooming || statusId == StatusStinging)
            {
                _flamespireAM.FeedStatus(dest, statusId, gained);
            }
        }

        private void UnsubscribeFromEvents()
        {
            _state.OnStatusChange -= OnStatusChange;
        }

        private void OnCombatChange(bool inCombat)
        {
            Reset();
            if (inCombat == true)
            {
                SubscribeToEvents();
            }
            else
            {
                UnsubscribeFromEvents();
            }
        }

        private void OnZoneChange(ushort newZone)
        {
            bool newZoneOk = (newZone == 1096);
            if (newZoneOk == true && ZoneOk == false)
            {
                Log(State.LogLevelEnum.Info, null, "Content available");
                _flamespireAM = (FlamespireAM)Items["FlamespireAM"];
                _state.OnCombatChange += OnCombatChange;
            }
            else if (newZoneOk == false && ZoneOk == true)
            {
                Log(State.LogLevelEnum.Info, null, "Content unavailable");
                _state.OnCombatChange -= OnCombatChange;
            }
            ZoneOk = newZoneOk;
        }
    }
}