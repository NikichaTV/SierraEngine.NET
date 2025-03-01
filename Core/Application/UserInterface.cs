using System.Numerics;
using ImGuiNET;
using SierraEngine.Core.Rendering.Vulkan;
using SierraEngine.Engine.Classes;
using GLFW;
using Window = SierraEngine.Core.Rendering.Window;

namespace SierraEngine.Core.Application;

public class UserInterface
{
    private const float RENDERER_INFO_PADDING = 10.0f;
    private float windowWidth, windowHeight;

    private void UpdateData(in Window window)
    {
        windowWidth = window.width;
        windowHeight = window.height;
    }
    
    public void Update(in Window window)
    {
        UpdateData(window);

        // UpdateViewport();
        
        // ImGui.ShowDemoWindow();

        UpdateRendererInfoOverlay();

        UpdateHierarchy();
    }

    private void UpdateViewport()
    {
        const ImGuiWindowFlags VIEWPORT_FLAGS = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove  | ImGuiWindowFlags.NoCollapse |
                                         ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoTitleBar;
        
        ImGui.SetNextWindowPos(new Vector2(0.0f, 18.0f));
        ImGui.SetNextWindowSize(new Vector2(windowWidth / 2, windowHeight / 2), ImGuiCond.Always);

        if (!ImGui.Begin("Main", VIEWPORT_FLAGS))
        {
            ImGui.End();
            return;
        }
        
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Menu"))
            {
                ImGui.MenuItem("Some menu");
                ImGui.MenuItem("Some menu");
                ImGui.MenuItem("Some menu");
                ImGui.MenuItem("Some menu");
                
                ImGui.EndMenu();
            }
            
            ImGui.EndMainMenuBar();
        }
    }

    private void UpdateRendererInfoOverlay()
    {
        const ImGuiWindowFlags WINDOW_FLAGS = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | 
                                              ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove
                                              | ImGuiWindowFlags.NoResize;
        
        ImGui.SetNextWindowPos(new Vector2(windowWidth - RENDERER_INFO_PADDING, RENDERER_INFO_PADDING), ImGuiCond.Always, new Vector2(1, 0));
        
        if (ImGui.Begin("Renderer Information", WINDOW_FLAGS))
        {
            ImGui.Text($"CPU Frame Time: { Time.FPS.ToString().PadLeft(4, '0') } FPS");
            ImGui.Text($"GPU Draw Time: { VulkanRendererInfo.drawTime:n6} ms");
            ImGui.Separator();
            ImGui.Text($"Total meshes being drawn: { VulkanRendererInfo.meshesDrawn }");
            ImGui.Text($"Total vertices in scene: { VulkanRendererInfo.verticesDrawn }");

            if (Input.GamepadConnected())
            {
                ImGui.Separator();
                ImGui.Text($"Gamepad [{ Input.GetGamepadName() }] properties:");
                ImGui.Text($"Left gamepad stick: [{ Input.GetGamepadLeftStickAxis().ToString("n4") }]");
                ImGui.Text($"Right gamepad stick: [{ Input.GetGamepadRightStickAxis().ToString("n4") }]");
                ImGui.Text($"Left trigger: [{ Input.GetGamepadLeftTriggerAxis():n4}]");
                ImGui.Text($"Right trigger: [{ Input.GetGamepadRightTriggerAxis():n4}]");
                ImGui.RadioButton($"\"A\" pressed", Input.GetGamepadButtonPressed(GamePadButton.A));
                ImGui.RadioButton($"\"A\" held", Input.GetGamepadButtonHeld(GamePadButton.A));
                ImGui.RadioButton($"\"A\" released", Input.GetGamepadButtonReleased(GamePadButton.A));
            }
        
            ImGui.End();
        }
    }

    private void UpdateHierarchy()
    {
        // Hierarchy
        ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoSavedSettings;
        
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.FirstUseEver, Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(windowWidth / 6, windowHeight), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(100f, 100f), new Vector2(windowWidth, windowHeight));
        
        if (ImGui.Begin("Hierarchy", windowFlags))
        {
            ImGui.Separator();
            
            foreach (GameObject gameObject in World.hierarchy)
            {
                if (gameObject.hasParent) continue;
                ListDeeper(gameObject, true);
            }

            ImGui.End();
        }
    }

    private void ListDeeper(in GameObject gameObject, in bool rootObject, in bool expandAll = false)
    {
        ImGuiTreeNodeFlags treeNodeFlag = (gameObject.selected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None) |
                                          ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.FramePadding |
                                          ImGuiTreeNodeFlags.SpanAvailWidth;

        if (expandAll) ImGui.SetNextItemOpen(true);
        
        if (ImGui.TreeNodeEx(gameObject.name, treeNodeFlag))
        { 
            bool clicked = ImGui.IsItemClicked();
            if (clicked)
            {
                if (World.selectedGameObject != null) World.selectedGameObject.selected = false;
                World.selectedGameObject = gameObject;
                World.selectedGameObject.selected = true;
            }
           
            bool nextExpandAll = expandAll || (clicked && Input.GetKeyHeld(Keys.LeftAlt));
       
            foreach (GameObject child in gameObject.children)
            {
                ListDeeper(child, false, nextExpandAll);
            }   

            ImGui.TreePop();
        }
        else
        {
            if (ImGui.IsItemClicked() && Input.GetKeyHeld(Keys.LeftAlt))
            {
                // LOGIC TO COLLAPSE THE WHOLE TREE
            }
        }
            
        if (rootObject) ImGui.Separator();
    }
}