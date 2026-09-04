# Scripts

| Path | Role |
|------|------|
| `Net/IronwakeClient.cs` | WS prefer → HTTP join/input/state poll 10–20 Hz; control-only input; events |
| `Combat/VehicleController.cs` | Prediction + soft-correct; FPS/chase (V); spectator; SpawnPrimitive by class |
| `Combat/ModuleDamagePresenter.cs` | All PROTOCOL modules; VFX hooks; UGUI strip + OnGUI |
| `Combat/ProjectileVisual.cs` | Server tracers + ProjectilePresenter |
| `Combat/BattleBootstrap.cs` | Wire client, local player, RemoteUnitView, match end |
| `Vehicles/VehicleCatalog.cs` | `GET /catalog/vehicles` + fallback |
| `Meta/HangarUI.cs` | Wallet + catalog list + Start Battle |
| `Meta/GoogleAuthPlaceholder.cs` | Guest / Google ID token notes |

Protocol sister: ironwake-server `PROTOCOL.md`. Live Beget requires HTTP poll.
