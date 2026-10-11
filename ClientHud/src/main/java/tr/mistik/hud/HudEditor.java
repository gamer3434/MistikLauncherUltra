package tr.mistik.hud;

import net.minecraft.client.gui.DrawContext;
import net.minecraft.client.gui.screen.Screen;
import net.minecraft.client.gui.widget.ButtonWidget;
import net.minecraft.text.Text;

final class HudEditor extends Screen {
    private final MistikHud hud;
    private boolean draggingMode;
    private int dragging = -1;
    private double offsetX, offsetY;
    HudEditor(MistikHud hud) { super(Text.literal("MISTIK HUD")); this.hud = hud; }
    @Override protected void init() {
        clearChildren();
        int left = width / 2 - 120, top = height / 2 - 100;
        if (draggingMode) {
            addDrawableChild(ButtonWidget.builder(Text.literal("Back / Geri"), b -> { draggingMode = false; init(); })
                .dimensions(width / 2 - 60, height - 30, 120, 20).build());
            return;
        }
        addDrawableChild(ButtonWidget.builder(Text.literal("HUD: " + (MistikHud.config.enabled ? "ON" : "OFF")), b -> {
            MistikHud.config.enabled = !MistikHud.config.enabled; MistikHud.save(); init();
        }).dimensions(left, top + 32, 240, 20).build());
        for (int i = 0; i < 6; i++) {
            final int index = i;
            addDrawableChild(ButtonWidget.builder(Text.literal(HudConfig.LABELS[i] + ": " + (MistikHud.config.widgets[i].enabled ? "ON" : "OFF")), b -> {
                MistikHud.config.widgets[index].enabled = !MistikHud.config.widgets[index].enabled;
                MistikHud.save(); init();
            }).dimensions(left + i % 2 * 124, top + 58 + i / 2 * 25, 116, 20).build());
        }
        addDrawableChild(ButtonWidget.builder(Text.literal("Move HUD / Yerleşim"), b -> { draggingMode = true; init(); })
            .dimensions(left, top + 137, 240, 20).build());
        addDrawableChild(ButtonWidget.builder(Text.literal("Scale -"), b -> resize(-.25f)).dimensions(left, top + 162, 75, 20).build());
        addDrawableChild(ButtonWidget.builder(Text.literal("Scale +"), b -> resize(.25f)).dimensions(left + 82, top + 162, 75, 20).build());
        addDrawableChild(ButtonWidget.builder(Text.literal("Reset"), b -> {
            MistikHud.config.widgets = HudConfig.defaults(); MistikHud.config.scale = 1; MistikHud.save(); init();
        }).dimensions(left + 164, top + 162, 76, 20).build());
        addDrawableChild(ButtonWidget.builder(Text.literal("Done / Tamam"), b -> close()).dimensions(left, top + 187, 240, 20).build());
    }
    private void resize(float amount) { MistikHud.config.scale = Math.clamp(MistikHud.config.scale + amount, .75f, 2f); MistikHud.save(); }
    @Override public void render(DrawContext draw, int mouseX, int mouseY, float delta) {
        draw.fill(0, 0, width, height, draggingMode ? 0x40000000 : 0xB0000000);
        if (draggingMode) {
            hud.render(draw, true);
            draw.drawCenteredTextWithShadow(textRenderer, "Drag widgets / Kutuları sürükle", width / 2, 12, 0xFFFFFFFF);
        } else {
            int left = width / 2 - 128, top = height / 2 - 108;
            draw.fill(left, top, left + 256, top + 224, 0xF0141414);
            draw.drawBorder(left, top, 256, 224, 0xFF555555);
            draw.drawCenteredTextWithShadow(textRenderer, title, width / 2, top + 14, 0xFFFFFFFF);
        }
        super.render(draw, mouseX, mouseY, delta);
    }
    @Override public boolean mouseClicked(double mx, double my, int button) {
        if (draggingMode && button == 0 && my < height - 35) {
            double sx = mx / MistikHud.config.scale, sy = my / MistikHud.config.scale;
            for (int i = 5; i >= 0; i--) {
                if (!MistikHud.config.widgets[i].enabled) continue;
                int x = MistikHud.x(i, width), y = MistikHud.y(i, height);
                if (sx >= x && sx < x + MistikHud.widgetWidth(i) && sy >= y && sy < y + MistikHud.HEIGHTS[i]) {
                    dragging = i; offsetX = sx - x; offsetY = sy - y; return true;
                }
            }
        }
        return super.mouseClicked(mx, my, button);
    }
    @Override public boolean mouseDragged(double mx, double my, int button, double dx, double dy) {
        if (dragging >= 0 && button == 0) {
            var w = MistikHud.config.widgets[dragging];
            w.x = (int)Math.max(0, mx / MistikHud.config.scale - offsetX);
            w.y = (int)Math.max(0, my / MistikHud.config.scale - offsetY);
            w.x = MistikHud.x(dragging, width); w.y = MistikHud.y(dragging, height);
            return true;
        }
        return super.mouseDragged(mx, my, button, dx, dy);
    }
    @Override public boolean mouseReleased(double mx, double my, int button) {
        if (dragging >= 0) { dragging = -1; MistikHud.save(); return true; }
        return super.mouseReleased(mx, my, button);
    }
    @Override public void close() { MistikHud.save(); super.close(); }
    @Override public boolean shouldPause() { return false; }
}
