package tr.mistik.hud.mixin;

import tr.mistik.hud.MistikHud;

import net.minecraft.client.Mouse;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(Mouse.class)
abstract class MouseMixin {
    @Inject(method = "onMouseButton", at = @At("HEAD"))
    private void mistikClick(long window, int button, int action, int modifiers, CallbackInfo ci) {
        MistikHud.recordClick(window, button, action);
    }
}
