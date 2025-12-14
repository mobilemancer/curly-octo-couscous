# Assumptions

1. Minimum period to rent a vehicle is one full day.
2. A driver can return a vehicle in a different time zone.
3. Rounding for final price presentation rounds up to nearest integer value.
4. An inital set of vehicles available for rental is available to the system on startup.
5. We're assuming user timezone is the same as system timezone, and will fetch this on app startup.
6. Vehicle License plates are unique.
7. There are vehicles that don't have license plates - we don't support those.
8. There are vehicles that don't rent by the km or day - we don't support those.
