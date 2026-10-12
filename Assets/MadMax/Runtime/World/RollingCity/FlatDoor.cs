using MadMax.Building;
using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The sign at a generated city flat's door: rent it, buy it, pay the next week (WastelandGame.Quarters).</summary>
    public class FlatDoor : MonoBehaviour, IInteractable
    {
        public CityHost city;
        public int room;

        void OnEnable() => PartFunctions.Interactables.Add(this);
        void OnDisable() => PartFunctions.Interactables.Remove(this);

        public string Prompt(WastelandGame game) => game.FlatPrompt(city, room);
        public void Use(WastelandGame game, bool secondary) => game.FlatUse(city, room, secondary);
    }
}
