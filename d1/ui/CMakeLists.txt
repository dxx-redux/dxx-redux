add_library(ui STATIC
    button.c
    checkbox.c
    dialog.c
    file.c
    gadget.c
    icon.c
    inputbox.c
    keypad.c
    keypress.c
    keytrap.c
    listbox.c
    menu.c
    menubar.c
    message.c
    popup.c
    radio.c
    scroll.c
    ui.c
    uidraw.c
    userbox.c
    )

include_directories(../include ../arch/include ../main)

find_package(sdl)
find_package(physfs)
target_include_directories(ui PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
