# Changelog

## Unreleased

### Desktop Organizer
- Added a custom desktop overlay so FocusPanel can collect desktop icons into panel partitions while the original files remain in the Desktop folder.
- Added free desktop icon placement with persisted `DesktopX` / `DesktopY` coordinates.
- Added drag support from desktop into the organizer panel and back out to the desktop without triggering Explorer's same-name file prompt.
- Added desktop icon sorting by name, type, date, and size.
- Expanded native-like desktop context menu actions: open, open with, show in Explorer, cut, copy, paste, rename, delete, refresh, properties, sorting, and desktop folder access.
- Added adjustable desktop icon size from the desktop right-click menu: small, medium, large, and extra large.
- Persisted desktop icon size in app configuration.
- Improved icon rendering quality by loading larger shell icons and using high-quality scaling.

### Desktop Storage Semantics
- Reworked collection behavior away from simply relying on Windows hidden-item visibility as the user-facing model.
- Kept files physically on the desktop while FocusPanel tracks whether they are collected into the panel.
- Updated desktop file scanning to include FocusPanel-managed collected files while still filtering unrelated system-hidden files.
- Added database schema support for desktop icon position and collection state.
- Fixed save failures caused by missing or null desktop file preference fields.

### UI
- Removed the Dashboard navigation entry from the main panel.
- Changed the default startup view to the desktop organizer page.
- Rebuilt the main shell with an Apple-inspired glassmorphism style: translucent surfaces, soft shadows, rounded panels, and lighter navigation.
- Restyled the desktop organizer page with glass cards, polished partition headers, clearer file hover/selection states, and localized Chinese UI copy.
- Improved the organizer toolbar, popups, empty state, rename dialog, and rescue tools presentation.
- Refined the Apple-inspired styling after review to reduce excessive transparency, gradients, and heavy shadows in favor of a cleaner Finder-like panel and organizer layout.
- Reworked the organizer layout after visual QA: removed the nested left action rail, moved organizer actions into a single top toolbar, unified icon sizing/color, and standardized partition card radius, borders, and drag feedback.
- Fixed right-edge drawer chrome: removed right-side rounded corners, removed outer shadow halo, disabled host-window DWM rounding, and made the drawer background fully opaque.

### Desktop-Only Panel Behavior
- Improved panel visibility behavior so it can stay available in the desktop scene while avoiding obstruction of normal foreground applications.
- Preserved drag-to-panel behavior while preventing the panel from disappearing during desktop collection workflows.
