package tr.mistik.hud;

import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.fabricmc.fabric.api.client.keybinding.v1.KeyBindingHelper;
import net.fabricmc.fabric.api.client.rendering.v1.HudRenderCallback;
import net.fabricmc.loader.api.FabricLoader;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.option.KeyBinding;
import net.minecraft.client.util.InputUtil;
import net.minecraft.item.ItemStack;
import net.minecraft.text.Text;
import org.lwjgl.glfw.GLFW;
import java.nio.file.Path;

public final class MistikHud implements ClientModInitializer {
    static final Path CONFIG_PATH = FabricLoader.getInstance().getConfigDir().resolve("mistik-hud.json");
    static HudConfig config = HudConfig.load(CONFIG_PATH);
    static final int[] WIDTHS = {76, 76, 76, 154, 70, 180};
    static final int[] HEIGHTS = {20, 20, 20, 20, 82, 26};
    private static MistikHud instance;
    private final ClickCounter[] clicks = {new ClickCounter(), new ClickCounter()};
    private final boolean[] mouseDown = new boolean[2];
    private final String[] values = {"0 FPS", "0 | 0 CPS", "0 ms", "0 / 64 / 0"};
    private final String[] cpsLabels = {"0 CPS", "0 CPS"};
    private final String[] durabilityLabels = {"-", "-", "-", "-"};
    private long nextRefresh;
    private static int coordinatesWidth = 154;
    private KeyBinding editor;

    @Override public void onInitializeClient() {
        instance = this;
        editor = KeyBindingHelper.registerKeyBinding(new KeyBinding("Mistik HUD Editor", InputUtil.Type.KEYSYM,
            GLFW.GLFW_KEY_RIGHT_SHIFT, "Mistik HUD"));
        ClientTickEvents.END_CLIENT_TICK.register(client -> {
            while (editor.wasPressed()) if (client.player != null && client.currentScreen == null) client.setScreen(new HudEditor(this));
        });
        HudRenderCallback.EVENT.register((context, delta) -> {
            MinecraftClient client = MinecraftClient.getInstance();
            if (client.player == null || client.options.hudHidden || client.currentScreen instanceof HudEditor) return;
            sampleClicks(client);
            if (config.enabled) render(context, false);
        });
    }
    public static void recordClick(long window, int button, int action) {
        var client = MinecraftClient.getInstance();
        if (instance != null && action == GLFW.GLFW_PRESS && button >= 0 && button < 2
            && window == client.getWindow().getHandle() && client.player != null && client.currentScreen == null)
            instance.clicks[button].record(System.currentTimeMillis());
    }
    private void sampleClicks(MinecraftClient client) {
        long now = System.currentTimeMillis();
        for (int button = 0; button < 2; button++) {
            boolean pressed = client.currentScreen == null && GLFW.glfwGetMouseButton(client.getWindow().getHandle(), button) == GLFW.GLFW_PRESS;
            mouseDown[button] = pressed;
        }
        if (now < nextRefresh) return;
        nextRefresh = now + 100;
        values[0] = client.getCurrentFps() + " FPS";
        int left = clicks[0].count(now), right = clicks[1].count(now);
        values[1] = left + " | " + right + " CPS";
        cpsLabels[0] = left + " CPS"; cpsLabels[1] = right + " CPS";
        var entry = client.getNetworkHandler() == null ? null : client.getNetworkHandler().getPlayerListEntry(client.player.getUuid());
        values[2] = (entry == null ? 0 : entry.getLatency()) + " ms";
        var pos = client.player.getBlockPos();
        values[3] = pos.getX() + " / " + pos.getY() + " / " + pos.getZ();
        coordinatesWidth = Math.max(154, client.textRenderer.getWidth(values[3]) + 12);
        for (int slot = 0; slot < 4; slot++) {
            ItemStack item = client.player.getInventory().armor.get(3 - slot);
            durabilityLabels[slot] = item.isDamageable() ? Math.clamp((item.getMaxDamage() - item.getDamage()) * 100L / item.getMaxDamage(), 0, 100) + "%" : "-";
        }
    }
    static int widgetWidth(int index) { return index == 3 ? coordinatesWidth : WIDTHS[index]; }
    static int x(int index, int width) { return Math.clamp(config.widgets[index].x, 0, Math.max(0, (int)(width / config.scale) - widgetWidth(index))); }
    static int y(int index, int height) { return Math.clamp(config.widgets[index].y, 0, Math.max(0, (int)(height / config.scale) - HEIGHTS[index])); }
    void render(DrawContext draw, boolean edit) {
        MinecraftClient client = MinecraftClient.getInstance();
        if (client.player == null) return;
        draw.getMatrices().push();
        draw.getMatrices().scale(config.scale, config.scale, 1);
        for (int i = 0; i < 6; i++) {
            if (!config.widgets[i].enabled) continue;
            int x = x(i, draw.getScaledWindowWidth()), y = y(i, draw.getScaledWindowHeight());
            if (i < 4) {
                draw.fill(x, y, x + widgetWidth(i), y + HEIGHTS[i], 0x99000000);
                draw.drawCenteredTextWithShadow(client.textRenderer, values[i], x + widgetWidth(i) / 2, y + 6, 0xFFFFFFFF);
            } else if (i == 4) {
                key(draw, x + 24, y, 22, 22, "W", client.options.forwardKey.isPressed());
                key(draw, x, y + 24, 22, 22, "A", client.options.leftKey.isPressed());
                key(draw, x + 24, y + 24, 22, 22, "S", client.options.backKey.isPressed());
                key(draw, x + 48, y + 24, 22, 22, "D", client.options.rightKey.isPressed());
                mouseKey(draw, x, y + 48, "LMB", 0);
                mouseKey(draw, x + 36, y + 48, "RMB", 1);
                key(draw, x, y + 74, 70, 8, "", client.options.jumpKey.isPressed());
                draw.fill(x + 22, y + 78, x + 48, y + 79, client.options.jumpKey.isPressed() ? 0xFF000000 : 0xFFFFFFFF);
            } else {
                draw.fill(x, y, x + WIDTHS[i], y + HEIGHTS[i], 0x99000000);
                for (int slot = 0; slot < 4; slot++) {
                    ItemStack armor = client.player.getInventory().armor.get(3 - slot);
                    int ax = x + 4 + slot * 44;
                    if (!armor.isEmpty()) {
                        draw.drawItem(armor, ax, y + 2);
                        if (armor.isDamageable()) {
                            int remaining = armor.getMaxDamage() - armor.getDamage();
                            draw.drawTextWithShadow(client.textRenderer, durabilityLabels[slot], ax + 17, y + 7, remaining < armor.getMaxDamage() / 4 ? 0xFFFF6969 : 0xFFFFFFFF);
                        }
                    } else draw.drawTextWithShadow(client.textRenderer, "-", ax + 6, y + 7, 0xFFAAAAAA);
                }
            }
            if (edit) draw.drawBorder(x, y, widgetWidth(i), HEIGHTS[i], 0xFFAAAAAA);
        }
        draw.getMatrices().pop();
    }
    private void mouseKey(DrawContext draw, int x, int y, String label, int button) {
        key(draw, x, y, 34, 24, "", mouseDown[button]);
        int color = mouseDown[button] ? 0xFF000000 : 0xFFFFFFFF;
        var font = MinecraftClient.getInstance().textRenderer;
        draw.drawCenteredTextWithShadow(font, label, x + 17, y + 3, color);
        draw.drawCenteredTextWithShadow(font, cpsLabels[button], x + 17, y + 14, color);
    }
    private void key(DrawContext draw, int x, int y, int width, int height, String label, boolean pressed) {
        draw.fill(x, y, x + width, y + height, pressed ? 0xEFFFFFFF : 0x99000000);
        if (!label.isEmpty()) draw.drawCenteredTextWithShadow(MinecraftClient.getInstance().textRenderer,
            label, x + width / 2, y + (height - 8) / 2, pressed ? 0xFF000000 : 0xFFFFFFFF);
    }
    static void save() {
        try { config.save(CONFIG_PATH); }
        catch (java.io.IOException | RuntimeException ex) {
            System.err.println("Mistik HUD: configuration save failed: " + ex.getMessage());
            var client = MinecraftClient.getInstance();
            if (client.player != null) client.player.sendMessage(Text.literal("Mistik HUD: settings could not be saved."), false);
        }
    }
}
