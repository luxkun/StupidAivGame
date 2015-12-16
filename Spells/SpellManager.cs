﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aiv.Engine;

namespace Futuridium.Spells
{
    public class SpellManager : GameObject
    {
        public delegate void SpellCdChangedEventHandler(object sender);

        private const float SwapDelay = 0.5f;
        private Dictionary<Type, List<Spell>> activeSpells;
        private int chosenSpellIndex = -1;
        private float lastSwapTimer;
        private int spellCounter;
        public Dictionary<Type, float> spellsCd;

        public SpellManager(Character owner)
        {
            Owner = owner;
            name = $"{owner.name}_spellManager";
        }

        public Type ChosenSpell { get; private set; }

        public Spell LastCastedSpell { get; private set; }

        public event SpellCdChangedEventHandler OnSpellCdChanged;

        private int ChosenSpellIndex
        {
            get { return chosenSpellIndex; }
            set
            {
                if (value != -1)
                {
                    chosenSpellIndex = value%Owner.Level.spellList.Count;
                    ChosenSpell = Owner.Level.spellList[chosenSpellIndex];
                }
                else
                {
                    chosenSpellIndex = value;
                }
            }
        }

        private Character Owner { get; }

        public string ChosenSpellName => (string)ChosenSpell.GetField("spellName").GetValue(null);

        public override void Start()
        {
            activeSpells = new Dictionary<Type, List<Spell>>();
            spellsCd = new Dictionary<Type, float>();

            UpdateSpells();
        }

        public override void Update()
        {
            if (Game.Instance.MainWindow != "game") return;
            bool cdChanged = false;
            foreach (var key in spellsCd.Keys.ToList())
            {
                if (spellsCd[key] > 0f)
                {
                    spellsCd[key] -= deltaTime;
                    cdChanged = true;
                }
            }
            if (cdChanged)
                OnSpellCdChanged?.Invoke(this);
            if (lastSwapTimer > 0)
                lastSwapTimer -= deltaTime;
        }


        public bool SpellOnCd(Type spellType = null)
        {
            if (spellType == null)
                spellType = ChosenSpell;
            var result = spellsCd[spellType] > 0;
            // if spell is alive and still casting
            if (!result && LastCastedSpell != null && LastCastedSpell.IsCasting)
                return true;
            return result;
        }

        public void SwapSpell()
        {
            if (Owner.Level.spellList.Count == 0 || lastSwapTimer > 0)
                return;
            lastSwapTimer = SwapDelay;
            ChosenSpellIndex++;
            OnSpellCdChanged?.Invoke(this);
            Debug.WriteLine($"Chosen spell '{ChosenSpell}'.");
        }

        public Spell ActivateSpell(Type spellType = null, Func<bool> castCheck = null)
        {
            if (SpellOnCd(spellType))
                return null;
            //    throw new Exception("Spell '$(spellType)' is on cold down.");
            if (spellType == null)
                spellType = ChosenSpell;
            // if is an activated spell and it's already casted disactivate it
            if (activeSpells[spellType].Count > 0 && activeSpells[spellType][0].ActivatedSpell)
            {
                spellsCd[spellType] = activeSpells[spellType][0].StartingCd;
                DisactivateSpell(activeSpells[spellType][0]);
                return null;
            }
            var spell = (Spell) Activator.CreateInstance(spellType);
            if (Owner.Level.Energy < spell.EnergyUsage)
                return null;
            spell.Owner = Owner;
            activeSpells[spellType].Add(spell);
            LastCastedSpell = spell;
            spell.CastCheck = castCheck;
            spell.OnDestroy += sender => DisactivateSpell(spell, false);
            spell.name = spell.RoomConstricted
                ? Game.Instance.CurrentFloor.CurrentRoom.name + "_"
                : "" + $"{name}_spell_{spell.SpellName}_{spellCounter++}";
            spell.order = order + 1;
            spellsCd[spellType] = spell.StartingCd;
            return spell;
        }

        public void DisactivateSpell(Spell spell, bool destroy = true)
        {
            if (!activeSpells[spell.GetType()].Contains(spell))
                return;
            activeSpells[spell.GetType()].Remove(spell);
            if (destroy)
                spell.Destroy();
        }

        public void UpdateSpells()
        {
            foreach (var type in Owner.Level.spellList)
            {
                if (!activeSpells.ContainsKey(type))
                    activeSpells[type] = new List<Spell>();
                if (!spellsCd.ContainsKey(type))
                    spellsCd[type] = 0f;
            }
            ChosenSpellIndex = -1;
            SwapSpell();
        }
    }
}