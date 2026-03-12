---
title: CharUco Board Creation
nav_order: 6
---

# CharUco Board Creation

**Status:** Currently generates only CharUco boards. Future work includes implementing a chessboard option for debugging corner detection.  
**Object:** Charuco  
**Script:** `ArUco`

## Goal
Generate a CharUco board texture.

## Execution
In Scene Mode, run it from the component options.

## Parameters
- **Target Raw Image** — UI element displaying the board
- **Dictionary Id**
- **Markers x** — Number of squares in x *(name should be updated)*
- **Markers y** — Number of squares in y *(name should be updated)*
- **Checker length** — Size of squares in meters
- **Marker length** — Size of markers in meters
- **Pixels per markers** — Marker resolution
- **Margin pixels** — Margin resolution
