# Tray application implementation

1. Add failing lifetime, activation and startup-registration contract tests.
2. Implement the application lifetime coordinator and bounded activation pipe.
3. Add stable packaged-launcher startup registration and transactional settings.
4. Integrate tray status/actions and application-owned startup/ticks/shutdown.
5. Add background settings and update user documentation.
6. Run focused/full synthetic tests, native isolated review and package checks.

The existing scheduler and game launcher remain the only execution path. No
service, VM setup, new quest runner or automatic schedule enabling is included.
