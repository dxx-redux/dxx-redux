#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <SDL.h>
#include "physfsx.h"
#include "pstypes.h"
#include "strutil.h"
#include "newmenu.h"
#include "text.h"
#include "net_udp.h"
#include "playsave.h"

char mp_preset_last_group[80];
const char *mp_preset_dir = "presets";

// returns d_malloc allocated filename or NULL on error
static char *create_preset_filename(const char *name)
{
	char *filename;

	if (!((filename = d_malloc(strlen(mp_preset_dir) + 1 + strlen(name) + 4 + 1))))
		return NULL;
	strcpy(filename, mp_preset_dir);
	strcat(filename, "/");
	strcat(filename, name);
	strcat(filename, ".ini");
	PHYSFSEXT_locateCorrectCase(filename);
	return filename;
}

// returns d_malloc allocated name or NULL on error. name is empty for defaults
static char *select_preset_file(int create)
{
	char **files;
	newmenu_item *opts;
	int files_count;
	int optnum;
	int citem;
	char *preset_file;

	if (!(files = PHYSFS_enumerateFiles(mp_preset_dir))) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot search for preset files.");
		return NULL;
	}

	files_count = 0;
	for (char **cur = files; *cur; cur++) {
		int l = strlen(*cur);
		if (l <= 4 || d_stricmp(*cur + l - 4, ".ini"))
			continue;
		files_count++;
	}

	if (!files_count) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "No presets found.");
		PHYSFS_freeList(files);
		return NULL;
	}

	if (!(opts = d_malloc((files_count + 1) * sizeof(opts[0])))) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot create list.");
		PHYSFS_freeList(files);
		return NULL;
	}

	optnum = 0;
	if (create) {
		opts[optnum].type = NM_TYPE_MENU;  opts[optnum].text = "Create new group..."; optnum++;
	} else {
		opts[optnum].type = NM_TYPE_MENU;  opts[optnum].text = "Defaults"; optnum++;
	}
	for (char **cur = files; *cur; cur++) {
		int l = strlen(*cur);
		if (l <= 4 || d_stricmp(*cur + l - 4, ".ini"))
			continue;
		(*cur)[l - 4] = 0; // remove ext
		opts[optnum].type = NM_TYPE_MENU;  opts[optnum].text = *cur; optnum++;
	}

	citem = newmenu_do1(NULL, "Select Preset Group to Load", optnum, opts, NULL, NULL, 0);

	if (!create && citem == 0) { // defaults
		if (!(preset_file = d_strdup("")))
			nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot allocate name.");
	} else if (citem >= 0) {
		char *name = opts[citem].text;
		if (!(preset_file = create_preset_filename(name)))
			nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot allocate name.");
	} else {
		preset_file = NULL;
	}

	d_free(opts);
	PHYSFS_freeList(files);

	return preset_file;
}

static void trim_line(char *line) {
	int len = strlen(line);
	while (len && isspace(line[len - 1]))
		len--;
	line[len] = 0;
}

static char *select_preset_section(char *filename)
{
	newmenu_item *opts;
	int optnum;
	int menu_ret;
	char *section;
	PHYSFS_file *f;
	int opts_size;
	char line[50], *p;

	opts_size = 8;
	if (!(opts = d_malloc(opts_size * sizeof(opts[0])))) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot create list.");
		return NULL;
	}

	if (!(f = PHYSFSX_openReadBuffered(filename))) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot open %s", filename);
		d_free(opts);
		return NULL;
	}

	optnum = 0;
	while ((PHYSFSX_fgets(line, sizeof(line), f))) {
		trim_line(line);
		if (line[0] != '[')
			continue;
		if ((p = strchr(line, ']')))
			*p = 0;
		if (optnum == opts_size) {
			newmenu_item *newopts;
			opts_size += opts_size >> 1;
			if (!(newopts = d_realloc(opts, opts_size * sizeof(opts[0])))) {
				nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot create list.");
				while (optnum)
					d_free(opts[--optnum].text);
				d_free(opts);
				PHYSFS_close(f);
				return NULL;
			}
			opts = newopts;
		}
		opts[optnum].type = NM_TYPE_MENU;  opts[optnum].text = d_strdup(line + 1); optnum++;
	}
	PHYSFS_close(f);

	if (!optnum) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "No presets found.");
		d_free(opts);
		return NULL;
	}

	menu_ret = newmenu_do1(NULL, "Select Preset to Load", optnum, opts, NULL, NULL, 0);

	section = menu_ret >= 0 ? d_strdup(opts[menu_ret].text) : NULL;

	while (optnum--)
		d_free(opts[optnum].text);

	d_free(opts);

	return section;
}

// returns 1 if found, 0 if not found
static int preset_find_section(PHYSFS_file *f, const char *section)
{
	char line[50], *p;

	while ((PHYSFSX_fgets(line, sizeof(line), f))) {
		trim_line(line);
		if (line[0] != '[')
			continue;
		if ((p = strchr(line, ']')))
			*p = 0;
		if (!strcmp(line + 1, section))
			return 1;
	}
	return 0;
}

void mp_preset_load(void)
{
	char *filename, *section;
	PHYSFS_file *f;
	char line[50], *p;

	if (!(filename = select_preset_file(0)))
		return;

	if (!*filename) { // load defaults
		net_udp_set_defaults();
		return;
	}

	if (!(section = select_preset_section(filename)))
		return;

	if (!(f = PHYSFSX_openReadBuffered(filename))) {
		nm_messagebox(TXT_ERROR, 1, TXT_OK, "Cannot open %s", filename);
		d_free(section);
		d_free(filename);
		return;
	}

	preset_find_section(f, section);

	net_udp_set_defaults();

	// load settings
	while ((PHYSFSX_fgets(line, sizeof(line), f))) {
		trim_line(line);
		if (line[0] == '[')
			break;
		parse_netgame_line(line, &Netgame);
	}

	PHYSFS_close(f);
	d_free(section);
	d_free(filename);
}

// returns d_malloc allocated platform dependent filename or NULL on error
static char *preset_real_name(const char *filename)
{
	const char *sep = PHYSFS_getDirSeparator();
	const char *p, *dir;
	char *realname;
	int numdirs;

	numdirs = 0;
	for (const char *p = filename; (p = strchr(p, '/')); p++)
		numdirs++;

	if (!(dir = PHYSFS_getRealDir(filename)))
		dir = PHYSFS_getWriteDir();
	if (!(realname = d_malloc(strlen(dir) + (numdirs + 1) * strlen(sep) + strlen(filename) - numdirs + 1)))
		return NULL;
	strcpy(realname, dir);

	// if name ends with dir separator remove it
	if (strlen(realname) >= strlen(sep) && !strcmp(realname + strlen(realname) - strlen(sep), sep))
		realname[strlen(realname) - strlen(sep)] = 0;

	// copy each /-separated part of filename to native separated realname
	p = filename;
	for (;;) {
		const char *np;
		char *op;
		if (!(np = strchr(p, '/')))
			np = p + strlen(p);
		strcat(realname, sep);
		op = realname + strlen(realname);
		memcpy(op, p, np - p);
		op[np - p] = 0;
		if (!*np)
			break;
		p = np + 1;
	}
	return realname;
}

// returns -1 on error, 0 if ok
static int preset_rename(const char *orgname, const char *newname)
{
	char *realorg, *realnew;
	int ret;
	if (!(realorg = preset_real_name(orgname)))
		return -1;
	if (!(realnew = preset_real_name(newname))) {
		d_free(realorg);
		return -1;
	}
	ret = remove(realnew);
	if (ret == 0)
		ret = rename(realorg, realnew);
	d_free(realnew);
	d_free(realorg);
	return ret;
}

#if 0
// returns -1 on error, 0 if ok
static int preset_delete_section(const char *filename, const char *section)
{
	char *tmpfilename;
	const char *dir;
	PHYSFS_file *fi, *fo;
	int copy;
	char line[80];

	if (!(dir = PHYSFS_getRealDir(filename)))
		return -1;
	if (!(tmpfilename = d_malloc(strlen(filename) + 4 + 1)))
		return -1;
	strcpy(tmpfilename, filename);
	strcat(tmpfilename, ".tmp");
	if (!(fi = PHYSFSX_openReadBuffered(filename))) {
		free(tmpfilename);
		return -1;
	}
	if (!(fo = PHYSFS_openWrite(tmpfilename))) {
		PHYSFS_close(fi);
		free(tmpfilename);
		return -1;
	}

	copy = 1;
	while ((PHYSFSX_fgets(line, sizeof(line), fi))) {
		if (line[0] == '[')
			copy = memcmp(line + 1, section, strlen(section)) || line[strlen(section) + 1] != ']';
		if (copy)
			PHYSFS_writeBytes(fo, line, strlen(line) + 1);
	}

	PHYSFS_close(fo);
	PHYSFS_close(fi);
	preset_rename(tmpfilename, filename);
	free(tmpfilename);
	return 0;
}
#endif

// returns -1 on error, 0 if ok
static int preset_write_section_settings(PHYSFS_file *f, const char *section)
{
	PHYSFS_writeBytes(f, "[", 1);
	PHYSFS_writeBytes(f, section, strlen(section));
	PHYSFS_writeBytes(f, "]\n", 2);
	write_netgame_settings(f, &Netgame);
	return 0;
}

// returns -1 on error, 0 if ok
static int preset_add_section(const char *filename, const char *section)
{
	char *tmpfilename;
	const char *dir;
	PHYSFS_file *fi, *fo;
	int copy;
	char line[80];
	int added = 0;

	if (!(fi = PHYSFSX_openReadBuffered(filename))) {
		// new file
		if (!(fo = PHYSFS_openWrite(filename)))
			return -1;
		preset_write_section_settings(fo, section);
		PHYSFS_close(fo);
		return 0;
	}

	if (!(dir = PHYSFS_getRealDir(filename))) {
		PHYSFS_close(fi);
		return -1;
	}
	if (!(tmpfilename = d_malloc(strlen(filename) + 4 + 1))) {
		PHYSFS_close(fi);
		return -1;
	}
	strcpy(tmpfilename, filename);
	strcat(tmpfilename, ".tmp");
	if (!(fo = PHYSFS_openWrite(tmpfilename))) {
		PHYSFS_close(fi);
		free(tmpfilename);
		return -1;
	}

	copy = 1;
	added = 0;
	while ((PHYSFSX_fgets(line, sizeof(line), fi))) {
		if (line[0] == '[') {
			char *p;
			if ((p = strchr(line, ']')))
				*p = 0;
			if (!added && d_stricmp(line + 1, section) >= 0) { // add if next section is alphabetically higher
				preset_write_section_settings(fo, section);
				added = 1;
			}
			copy = strcmp(line + 1, section) != 0;
			if (p)
				*p = ']';
		}
		if (copy)
			PHYSFS_writeBytes(fo, line, strlen(line));
	}

	if (!added)
		preset_write_section_settings(fo, section);

	PHYSFS_close(fo);
	PHYSFS_close(fi);
	preset_rename(tmpfilename, filename);
	free(tmpfilename);
	return 0;
}

void mp_preset_save(void)
{
	newmenu_item m[5];
	int optnum;
	int menu_ret;
	char preset_name[80];
	char *filename;
	PHYSFS_file *f;

	if (!mp_preset_last_group[0])
		snprintf(mp_preset_last_group, sizeof(mp_preset_last_group), "%s", Players[Player_num].callsign);

	preset_name[0] = 0;

	optnum = 0;
	m[optnum].type = NM_TYPE_TEXT;  m[optnum].text="Preset Group"; optnum++;
	m[optnum].type = NM_TYPE_INPUT; m[optnum].text=mp_preset_last_group; m[optnum].text_len=sizeof(mp_preset_last_group)-1; optnum++;
	m[optnum].type = NM_TYPE_TEXT;  m[optnum].text="Preset Name"; optnum++;
	m[optnum].type = NM_TYPE_INPUT; m[optnum].text=preset_name; m[optnum].text_len=sizeof(preset_name)-1; optnum++;
	m[optnum].type = NM_TYPE_MENU;  m[optnum].text="Save"; optnum++;
	Assert(optnum <= SDL_arraysize(m));
	menu_ret = newmenu_do1(NULL, "Save Preset", optnum, m, NULL, NULL, 0);

	filename = create_preset_filename(mp_preset_last_group);
	//printf("real filename %s\n", preset_real_name(filename));
	//d_free(filename);

	if ((f = PHYSFSX_openReadBuffered(filename))) {
		if (preset_find_section(f, preset_name)) {
			if (nm_messagebox(NULL, 2, TXT_YES, TXT_NO, "Overwrite preset %s?", preset_name) != 0) {
				d_free(filename);
				return;
			}
		}
		PHYSFS_close(f);
	}

	preset_add_section(filename, preset_name);

	d_free(filename);
}
