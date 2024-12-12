
# Unity Boids Simulation Project

## Overview
This project demonstrates a GPU-accelerated boids simulation using compute shaders. The boids interact with their environment based on customizable swarm behaviors such as alignment, cohesion, and separation. It includes support for obstacle avoidance and food attractors, with optional XR passthrough functionality to treat scanned 3D environments as obstacles for the boids, supporting stereo instanced rendering.

## Features
- **Swarm Behavior**: Supports multiple swarms with adjustable parameters for separation, alignment, and cohesion.
- **Obstacle Avoidance**: Boids avoid user-defined obstacles dynamically.
- **Food Attraction**: Boids (prey and omnivores) are attracted to food attractors.
- **GPU-Powered**: Utilizes compute shaders for highly efficient boid behavior simulation.
- **VR and Non-VR Modes**: Includes separate scenes for VR and standard gameplay.

## Scenes
- **Non-VR Demo**: `Assets/Scenes/BoidsSampleScene.unity`
- **VR Demo**: `Assets/Scenes/BoidsXRDemo.unity`

## Project Structure
```
Assets/
├── Scenes/
│   ├── BoidsSampleScene.unity         # Standard demo scene
│   ├── BoidsXRDemo.unity              # VR-enabled demo scene
├── Scripts/
│   ├── SwarmManager.cs                # Main simulation and management script
├── Shaders/
│   ├── BoidComputeShader.compute      # Compute shader for boid behavior
│   ├── BoidShader.shader              # Vertex and fragment shader for rendering
├── Prefabs/
│   ├── FoodAttractor.prefab           # Prefab for food attractor objects
├── Materials/
│   ├── BoidMaterial.mat               # Material for boid rendering
```

## Requirements
- Unity Editor 6000.0.28f1  LTS or later
- Compatible GPU supporting compute shaders

### Optional for VR:
- XR Interaction Toolkit (Unity package)
- VR headset with compatible runtime

## Setup Instructions

### 1. Clone or Download the Repository
Download the repository or clone it using:
```bash
git clone <repository-url>
```

### 2. Open the Project in Unity
- Open Unity Hub and select **Open Project**.
- Navigate to the downloaded repository and select the root folder.

### 3. Configure Scenes
- **Non-VR Demo**: Open `Assets/Scenes/BoidsSampleScene.unity`.
- **VR Demo**: Open `Assets/Scenes/BoidsXRDemo.unity`.

### 4. Play the Simulation
- Press `Play` in the Unity Editor to run the simulation.

## Usage
### Simulation Controls (Non-VR)
- Add a boid to the selected swarm.
- Remove a boid from the selected swarm.
- Adjust parameters dynamically.

## Customization

### Adjust Swarm Parameters
Edit `SwarmParameters` in the **Inspector** to modify:
- **Separation Weight**: Influence of avoidance behavior.
- **Alignment Weight**: Influence of velocity matching.
- **Cohesion Weight**: Influence of grouping behavior.
- **Speed**: Adjust the speed of the boids.

### Add Obstacles
- Tag any GameObject as `"Obstacle"` to include it in obstacle avoidance calculations.

### Add Food Attractors
- Use the `SwarmManager` script to programmatically spawn attractors at desired locations.

## Contributing
Contributions are welcome! If you encounter bugs or have feature requests, feel free to open an issue or submit a pull request.

## License
This project is licensed under the MIT License. See `LICENSE` for details.
