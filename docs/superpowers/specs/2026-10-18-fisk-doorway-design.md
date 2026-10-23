# Fisk doorway rejection recovery

The requested fix covers the October 18 Bertil-delivery pause. The server
rejected one west step from Suomi (16,86) into the recorded entrance (15,86),
then confirmed the same position on refresh. No dialog or entrance occupant
was observed. The rejected target entered the walker's local avoidance set;
refreshing retained that exclusion and reported occupancy without another step.
The underlying server rejection reason remains unknown.

Keep a recorded exit target available after a rejected step, while retaining
avoidance for intermediate obstacles. Existing acknowledgement/refresh barriers
must settle movement before another submission. Keep the existing three-failure
limit, fresh entity collision checks, entry-refusal gates, generation isolation
and actual destination-map receipt. Do not modify quest transactions, paid waits
or saved player Data. A rejection with actual doorway occupancy must still stop.

Use synthetic loopback regressions for ordinary and fast movement: one rejection
followed by successful entry, three persistent rejections, and an occupant
appearing before retry. Run the related travel/Fisk tests and both full xUnit
projects, then build an isolated combined executable. No real characters or
running clients are operated for validation.
