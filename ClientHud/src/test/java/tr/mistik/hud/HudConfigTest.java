package tr.mistik.hud;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.Files;
import java.nio.file.Path;
import static org.junit.jupiter.api.Assertions.*;

class HudConfigTest {
    @TempDir Path temp;
    @Test void boundsAndMissingWidgetsRecover() {
        HudConfig c = new HudConfig(); c.scale = Float.NaN; c.widgets[0] = null;
        c.widgets[1].x = -99; c.widgets[1].y = Integer.MAX_VALUE; c.normalize();
        assertEquals(1f, c.scale); assertNotNull(c.widgets[0]); assertEquals(0, c.widgets[1].x); assertEquals(8192, c.widgets[1].y);
        c.scale = 8; c.widgets = new HudConfig.Widget[1]; c.normalize();
        assertEquals(2f, c.scale); assertEquals(6, c.widgets.length);
    }
    @Test void roundTripPreservesPositionsAndToggles() throws Exception {
        Path p = temp.resolve("mistik-hud.json"); HudConfig c = new HudConfig();
        c.enabled = false; c.widgets[4].enabled = false; c.widgets[2].x = 120; c.scale = 1.5f; c.save(p);
        HudConfig loaded = HudConfig.load(p);
        assertFalse(loaded.enabled); assertFalse(loaded.widgets[4].enabled); assertEquals(120, loaded.widgets[2].x); assertEquals(1.5f, loaded.scale);
    }
    @Test void malformedConfigurationFallsBack() throws Exception {
        Path p = temp.resolve("mistik-hud.json"); Files.writeString(p, "{broken");
        assertEquals(6, HudConfig.load(p).widgets.length);
        Files.writeString(p, "null"); assertTrue(HudConfig.load(p).enabled);
    }
}
