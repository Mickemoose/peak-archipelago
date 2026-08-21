
using System;
using System.Collections.Generic;
using System.Linq;

namespace Peak.AP
{
    public enum TrapType
    {
        BananaPeelTrap,
        Coconut,
        Dynamite,
        SpawnBeeSwarm,
        DestroyHeldItem,
        SwapTrap,
        MinorPoisonTrap,
        PoisonTrap,
        DeadlyPoisonTrap,
        TornadoTrap,
        NapTimeTrap,
        BalloonTrap,
        HungryHungryCamperTrap,
        CactusBallTrap,
        FreezeTrap,
        SlipTrap,
        YeetTrap,
        TumbleweedTrapEffect,
        ZombieHordeTrap,
        GustTrap,
        MandrakeTrap,
        FungalInfectionTrap,
        InstantDeathTrap,
        ItemsToBombs,
        PokemonTriviaTrap,
        BlackoutTrap,
        FearTrap,
        TurnToStoneTrap,
        ScoutmasterTrap,
        ZoomTrap,
        ScreenFlipTrap,
        DropEverythingTrap,
        PixelTrap,
        EruptionTrap,
        BeetleHordeTrap,
        CustomTriviaTrap,
        PokemonCountTrap,
        InvertedMouseTrap,
        StaminaDrainTrap,
        ChaosControlTrap,
        EmergencyRescueTrap,
        ExplosionTrap,
        FrogTrap,
        GhostTrap,
        RainTrap,
        PoisonCloudTrap,
        FrostCloudTrap,
        SleepTrap,
        WellDoneTrap,
        InstantCrystalTrap,
        CurseTrap,
        CursedBallTrap,
        StormTrap,
        SkeletonTrap,
    }

    public static class TrapTypeExtensions
    {
        private static readonly Dictionary<TrapType, string> _trapNames = new Dictionary<TrapType, string>
        {
            { TrapType.BananaPeelTrap, "Banana Peel Trap" },
            { TrapType.Coconut, "Coconut" },
            { TrapType.Dynamite, "Dynamite" },
            { TrapType.SpawnBeeSwarm, "Spawn Bee Swarm" },
            { TrapType.DestroyHeldItem, "Destroy Held Item" },
            { TrapType.SwapTrap, "Swap Trap" },
            { TrapType.MinorPoisonTrap, "Minor Poison Trap" },
            { TrapType.PoisonTrap, "Poison Trap" },
            { TrapType.DeadlyPoisonTrap, "Deadly Poison Trap" },
            { TrapType.TornadoTrap, "Tornado Trap" },
            { TrapType.NapTimeTrap, "Nap Time Trap" },
            { TrapType.BalloonTrap, "Balloon Trap" },
            { TrapType.HungryHungryCamperTrap, "Hungry Hungry Camper Trap" },
            { TrapType.CactusBallTrap, "Cactus Ball Trap" },
            { TrapType.FreezeTrap, "Freeze Trap" },
            { TrapType.SlipTrap, "Slip Trap" },
            { TrapType.YeetTrap, "Yeet Trap" },
            { TrapType.TumbleweedTrapEffect, "Tumbleweed Trap" },
            { TrapType.ZombieHordeTrap, "Zombie Horde Trap" },
            { TrapType.GustTrap, "Gust Trap" },
            { TrapType.MandrakeTrap, "Mandrake Trap" },
            { TrapType.FungalInfectionTrap, "Fungal Infection Trap" },
            { TrapType.TurnToStoneTrap, "Turn To Stone Trap" },
            { TrapType.InstantDeathTrap, "Instant Death Trap" },
            { TrapType.ItemsToBombs, "Items to Bombs" },
            { TrapType.PokemonTriviaTrap, "Pokemon Trivia Trap" },
            { TrapType.BlackoutTrap, "Blackout Trap" },
            { TrapType.FearTrap, "Fear Trap" },
            { TrapType.ScoutmasterTrap, "Scoutmaster Trap" },
            { TrapType.ZoomTrap, "Zoom Trap" },
            { TrapType.ScreenFlipTrap, "Screen Flip Trap" },
            { TrapType.DropEverythingTrap, "Drop Everything Trap" },
            { TrapType.PixelTrap, "Pixel Trap" },
            { TrapType.EruptionTrap, "Eruption Trap" },
            { TrapType.BeetleHordeTrap, "Beetle Horde Trap" },
            { TrapType.CustomTriviaTrap, "Custom Trivia Trap" },
            { TrapType.PokemonCountTrap, "Pokemon Count Trap" },
            { TrapType.InvertedMouseTrap, "Inverted Mouse Trap" },
            { TrapType.StaminaDrainTrap, "Stamina Drain Trap" },
            { TrapType.ChaosControlTrap, "Chaos Control Trap" },
            { TrapType.EmergencyRescueTrap, "Emergency Rescue Trap" },
            { TrapType.ExplosionTrap, "Explosion Trap" },
            { TrapType.FrogTrap, "Frog Trap" },
            { TrapType.GhostTrap, "Ghost Trap" },
            { TrapType.RainTrap, "Rain Trap" },
            { TrapType.PoisonCloudTrap, "Poison Cloud Trap" },
            { TrapType.FrostCloudTrap, "Frost Cloud Trap" },
            { TrapType.SleepTrap, "Sleep Trap" },
            { TrapType.WellDoneTrap, "Well Done Trap" },
            { TrapType.InstantCrystalTrap, "Instant Crystal Trap" },
            { TrapType.CurseTrap, "Curse Trap" },
            { TrapType.CursedBallTrap, "Cursed Ball Trap" },
            { TrapType.StormTrap, "Storm Trap" },
            { TrapType.SkeletonTrap, "Skeleton Trap" },
        };
        private static readonly Dictionary<string, TrapType> _nameToTrap = 
            _trapNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        public static string ToTrapName(this TrapType trapType)
        {
            return _trapNames.TryGetValue(trapType, out string name) ? name : trapType.ToString();
        }
        public static bool TryParseTrapName(string trapName, out TrapType trapType)
        {
            return _nameToTrap.TryGetValue(trapName, out trapType);
        }
        public static bool IsTrapName(string itemName)
        {
            return _nameToTrap.ContainsKey(itemName);
        }
        public static HashSet<string> GetAllTrapNames()
        {
            return [.. _trapNames.Values];
        }
        public static IEnumerable<TrapType> GetAllTraps()
        {
            return Enum.GetValues(typeof(TrapType)).Cast<TrapType>();
        }
    }
}