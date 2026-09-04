# Scripts

| Path | Role |
|------|------|
| `Sim/LocalBattleSim.cs` | Device-authoritative 20 Hz battle (default) |
| `Sim/LocalBotAI.cs` | Fill room with bots |
| `Sim/ModuleSystem.cs` | Module HP, pen/facing, fire/cook-off |
| `Graphics/BattleEnvironmentBuilder.cs` | Runtime ground/hills/props/lighting/fog |
| `Graphics/TankVisualBuilder.cs` | Multi-primitive military vehicles |
| `Graphics/CombatVfx.cs` | Muzzle, tracer, impact, fire, cook-off, dust |
| `Graphics/UrpVisualTuner.cs` | Bloom/vignette/color or Quality+HDR fallback |
| `Input/MobileBattleInput.cs` | Virtual stick + fire + aim drag |
| `Combat/BattleBootstrap.cs` | Default LocalSim; optional Online |
| `Combat/VehicleController.cs` | LocalSim snapshots or online prediction |
| `Combat/ModuleDamagePresenter.cs` | Module visuals + UI strip |
| `Combat/ProjectileVisual.cs` | Tracers |
| `Net/IronwakeClient.cs` | Meta + optional rooms + `POST /match` |
| `Meta/HangarUI.cs` | Local Battle (primary) + Online |
| `Meta/GoogleAuthPlaceholder.cs` | Guest / Google notes |
| `Vehicles/VehicleCatalog.cs` | `GET /catalog/vehicles` + fallback |

Server is meta only. Combat/graphics run on device.
