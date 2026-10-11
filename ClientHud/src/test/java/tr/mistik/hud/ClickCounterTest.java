package tr.mistik.hud;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class ClickCounterTest {
    @Test void expiresAndIgnoresFutureTimestamps() {
        var c = new ClickCounter(); c.record(1000); c.record(1500); c.record(2500);
        assertEquals(2, c.count(1800)); assertEquals(1, c.count(2500)); assertEquals(0, c.count(3500));
    }
    @Test void boundsStorageUnderExtremeInput() {
        var c = new ClickCounter(); for (int i = 0; i < 10000; i++) c.record(10000);
        assertEquals(128, c.count(10000));
    }
}
