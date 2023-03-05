using System;
using System.Collections.Generic;
using BepInEx;
using KSP;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using KSP.Sim.State;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;

namespace AutoBurn;

[BepInPlugin(ModGuid, ModName, ModVer)]
public class AutoBurn : BaseSpaceWarpPlugin
{
    public const string ModGuid = "com.github.cheese3660.autoburn";
    public const string ModName = "Auto Burn";
    public const string ModVer = "1.0.0";

    private bool _toggled = false;
    private bool _inManeuverNode = false;
    private VesselComponent _activeVessel;
    private ManeuverPlanComponent _activePlan;
    private ManeuverNodeData _activeNode;
    private bool _throttleSet = false;

    public override void OnInitialized()
    {
        Appbar.RegisterAppButton(
            "Auto Burn",
            "BTN-AutoBurn",
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            SetEnabled
        );
        
    }


    public void SetEnabled(bool toggled)
    {
        _toggled = toggled;
        if (!toggled)
        {
            _inManeuverNode = false;
            if (_activePlan != null) _activePlan.OnManeuverNodesRemoved -= OnNodeRemoved;
            _activePlan = null;
            _throttleSet = false;
        }
        GameObject.Find("BTN-AutoBurn")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(toggled);
        Game.Messages.Subscribe<VesselChangedMessage>(OnActiveVesselChanged);
    }

    public void OnNodeRemoved(List<ManeuverNodeData> guid)
    {
        SetEnabled(false);
    }

    public void OnActiveVesselChanged(MessageCenterMessage msg)
    {
        SetEnabled(false);
    }

    private static void SetThrottle(float throttle)
    {
        if (!Game.ViewController.TryGetActiveVehicle(out var vehicle)) return;
        var asVehicle = vehicle as VesselVehicle;
        var update = new FlightCtrlStateIncremental
        {
            mainThrottle = throttle
        };
        asVehicle!.AtomicSet(update);
    }
    private void LateUpdate()
    {
        if (!_toggled) return;
        if (_inManeuverNode)
        {
            var currentTime = Game.UniverseModel.UniversalTime;
            if (!(_activeNode.Time <= currentTime)) return;
            if (_activeNode.Time + _activeNode.BurnDuration <= currentTime)
            {
                SetThrottle(0);
                SetEnabled(false);
            } else
            {
                SetThrottle(1);
                _throttleSet = true;
            }
        }
        else
        {
            // Wait for a maneuver node
            if (!Game.ViewController.TryGetActiveSimVessel(out var vessel)) return;
            _activeVessel = vessel;
            _activePlan = _activeVessel.SimulationObject.FindComponent<ManeuverPlanComponent>();
            if (_activePlan == null) return;
            _activeNode = _activePlan.ActiveNode;
            if (_activeNode == null) return;
            _activeVessel.SetAutopilotEnableDisable(true);
            _activeVessel.SetAutopilotMode(AutopilotMode.Maneuver);
            _inManeuverNode = true;
            _activePlan.OnManeuverNodesRemoved += OnNodeRemoved;
        }

    }
}