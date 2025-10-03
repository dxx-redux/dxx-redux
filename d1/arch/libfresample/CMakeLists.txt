cmake_minimum_required(VERSION 3.8)

project(libfresample)


add_library(libfresample STATIC
    lib/convert.h
    lib/cpu.h
    lib/defs.h
    lib/filter.h
    lib/param.h
    lib/resample.h

    lib/cpu.c
    lib/filter_delay.c
    lib/filter_free.c
    lib/filter_get.c
    lib/filter_new.c
    lib/filter_new_window.c
    lib/info_name.c
    lib/param.c
    lib/param_name.c
    lib/resample.c
    lib/resample_s16func.c
    lib/resample_s16n1f32_scalar.c
    lib/resample_s16n1f32_sse2.c
    lib/resample_s16n1s16_scalar.c
    lib/resample_s16n1s16_sse2.c
    lib/resample_s16n2f32_scalar.c
    lib/resample_s16n2f32_sse2.c
    lib/resample_s16n2s16_scalar.c
    lib/resample_s16n2s16_sse2.c
    lib/swap16.c
    lib/swap16_scalar.c)

target_include_directories(libfresample PUBLIC include)
