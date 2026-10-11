package tr.mistik.hud;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;

public final class HudConfig {
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    public boolean enabled = true;
    public float scale = 1;
    public Widget[] widgets = defaults();
    public static final String[] LABELS = {"FPS", "CPS", "PING", "XYZ", "KEYSTROKES", "ARMOR"};
    public static final class Widget {
        public boolean enabled = true;
        public int x, y;
        public Widget(int x, int y) { this.x = x; this.y = y; }
    }
    public static Widget[] defaults() {
        return new Widget[] {new Widget(8, 8), new Widget(8, 31), new Widget(8, 54),
            new Widget(8, 77), new Widget(8, 100), new Widget(8, 205)};
    }
    public void normalize() {
        if (!Float.isFinite(scale)) scale = 1;
        scale = Math.clamp(scale, .75f, 2f);
        Widget[] fallback = defaults();
        if (widgets == null || widgets.length != fallback.length) widgets = fallback;
        for (int i = 0; i < widgets.length; i++) {
            if (widgets[i] == null) widgets[i] = fallback[i];
            widgets[i].x = Math.clamp(widgets[i].x, 0, 8192);
            widgets[i].y = Math.clamp(widgets[i].y, 0, 8192);
        }
    }
    public static HudConfig load(Path path) {
        if (!Files.isRegularFile(path)) return new HudConfig();
        try {
            if (Files.size(path) > 65536) throw new IOException("HUD config too large");
            HudConfig config = GSON.fromJson(Files.readString(path), HudConfig.class);
            if (config == null) return new HudConfig();
            config.normalize();
            return config;
        } catch (IOException | RuntimeException ex) {
            System.err.println("Mistik HUD: cannot read configuration: " + ex.getMessage());
            return new HudConfig();
        }
    }
    public void save(Path path) throws IOException {
        normalize();
        Files.createDirectories(path.getParent());
        Path temp = path.resolveSibling(path.getFileName() + ".tmp");
        Files.writeString(temp, GSON.toJson(this));
        try { Files.move(temp, path, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE); }
        catch (java.nio.file.AtomicMoveNotSupportedException ex) { Files.move(temp, path, StandardCopyOption.REPLACE_EXISTING); }
    }
}
