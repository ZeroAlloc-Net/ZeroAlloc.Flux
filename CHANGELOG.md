# Changelog

## [1.1.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/compare/v1.0.0...v1.1.0) (2026-05-26)


### Features

* **sample:** console end-to-end demo ([a3977bc](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/a3977bc094657f757d217b0553d9b145f6d91243))


### Tests

* **aot:** smoke binary exercises one feature end-to-end under PublishAot=true ([c51b8cc](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/c51b8ccbf3be2aa07cd73933ef13ab7d874fd2ec))

## 1.0.0 (2026-05-26)


### Features

* **blazor:** add AddZeroAllocFluxBlazor DI extension stub ([0dd62ab](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/0dd62ab9047ca105d4d01f63ab474ecea9e2f149))
* **blazor:** add FluxComponent base class ([06787ee](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/06787ee509662bdaa1828a304763efa527f22e72))
* **core:** add [Feature] attribute ([d988f63](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/d988f637f519e9e87c39762cafee095e22ae420f))
* **core:** add [Reducer] attribute ([26137f1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/26137f16ab5325f837beecab9bc629a304900e3d))
* **core:** add IDispatcher interface ([77a6166](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/77a61663ab71b5723607c01b208d34a370deea7c))
* **core:** add IStore&lt;TState&gt; interface ([dc52874](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/dc52874a5ddc629dab30e5e1ee6c1c79a84c5d8f))
* **generator:** declare ZFLUX001-ZFLUX005 diagnostic descriptors ([6257090](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/62570904f0dd1bba066d8da596abb117dab14a12))
* **generator:** discover [Feature]-decorated types via ForAttributeWithMetadataName ([7ba587b](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/7ba587b7bd7cb12bda62b6f25b5f045859372e11))
* **generator:** discover [Reducer] methods + validate signatures ([5f045c8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/5f045c85206fded077ec118fe9c841f8e1c5a720))
* **generator:** emit AddZeroAllocFlux DI extension ([2052815](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/205281555be0091e2bebd0ae31cae32c55eafe28))
* **generator:** emit IDispatcher implementation with fan-out ([1a9b6c2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/1a9b6c22fe705b608c8f1355a3b82ee619d99c0b))
* **generator:** emit per-feature Store&lt;TState&gt; implementations ([62523a8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/62523a8e88be559fe76d253187cbdd917afd75fe))
* **generator:** validate [Feature(InitialState=...)] factory signature ([49d74c8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/49d74c8bfc3031617f12c69fa6c2208958fc8d84))
* **generator:** wire IIncrementalGenerator entry point ([a717bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/a717bca7e7a2d055f4506ea40b1ce532b1723583))


### Bug Fixes

* **build:** drop PackageIcon reference until Flux-themed icon ships ([a1de004](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/a1de004cb494d474d66f83395fb606929ac726f4))
* **ci:** add gitversion tool manifest + remove generator pack steps ([f475ded](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/f475ded7e4fd8107ea4adef92d452f9ffe26d454))


### Documentation

* add README with quick-start + badges ([0a1a680](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/0a1a680add9612925cf7590ce6876fd3f24b73f5))
* initial design for ZeroAlloc.Flux v1.0.0 ([04c67cb](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/04c67cbdde87ce325a2a1b3e6b6d8bd2a546a591))
* **plans:** implementation plan for ZeroAlloc.Flux v1.0.0 ([d7003cb](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/d7003cb7049904a566f1f9bb9bcab140b184844e))


### Tests

* **alloc:** assert 0 B budgets on DispatchAsync sync path + Value getter ([847dfc2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/847dfc26f0bed0b037d51014335d44afa18d41a9))
* **blazor:** FluxComponent re-renders on store update via bunit ([b19f81d](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/b19f81d5571d2c1a355296ac931051634756c9ab))
* **blazor:** FluxComponent unsubscribes on Dispose ([5a12c54](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/5a12c54f48d43b8941a783645e98fc2d4e5357f9))
* **generator:** add TestHarness for VerifyXunit snapshot tests ([a13edb3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/a13edb39290b965e7b86d12b5bf6f513640de420))
* **generator:** assert clean source emits no ZFLUX diagnostics ([bb8a1c2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/bb8a1c2be05ec3f95d792dc3c9c9e78e4684bdde))
* **runtime:** assert class-state feature updates via CAS path ([b06ff55](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/b06ff555e56df3708f3aa803c97ad3ef9d249f35))
* **runtime:** assert DispatchAsync fan-out across matching features ([1ce2e8e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/1ce2e8e722402dcebbb2197ca04c460127850df9))
* **runtime:** assert StateChanged fires exactly once per dispatch ([0d5976d](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/0d5976d1a79dd888cdd496ee69cad515f5121d78))
* **runtime:** wire DispatchAsync&lt;TAction&gt; route to concrete overloads ([7a2f96d](https://github.com/ZeroAlloc-Net/ZeroAlloc.Flux/commit/7a2f96d200b36ab8bb76c7ee4c5cff04a6dba1c1))
