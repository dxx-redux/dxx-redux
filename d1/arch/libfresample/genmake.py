import os
from io import StringIO

ALL_SPECIAL = 'altivec mmx sse sse2 sse3 ssse3 sse4_1 sse4_2'.split()

def get_special(name):
    for s in ALL_SPECIAL:
        if name.endswith(s):
            return s
    return None

HEADER = """\
depfiles := $(wildcard @adir@/*/*.d)
ifneq ($(depfiles),)
include $(depfiles)
endif

all_dirs@asfx@ := @adir@/shared @adir@/static @adir@/tool
missing_dirs@asfx@ := $(filter-out $(wildcard $(all_dirs@asfx@)),$(all_dirs@asfx@))
$(all_dirs@asfx@):
\tmkdir -p $@

"""

FOOTER = """\

ifeq ($(UNAME_S),Darwin)

@adir@/libfresample.a: $(static_obj@asfx@)
\tlibtool -static -o $@ $^

@adir@/$(so_name): $(static_obj@asfx@)
\t$(CC@asfx@) -o $@ -dynamiclib -install_name /Library/Frameworks/fresample.framework/Versions/A/fresample -compatibility_version $(ver_maj).$(ver_min) -current_version $(ver_maj).$(ver_min) $(LDFLAGS@asfx@) $^ $(LIBS@asfx@)

else

@adir@/libfresample.a: $(static_obj@asfx@)
\trm -f $@
\tar rc $@ $^
\tranlib $@

@adir@/$(so_name): $(shared_obj@asfx@)
\t$(CC@asfx@) -o $@ -shared -Wl,-soname,$(so_name1) $(LDFLAGS@asfx@) $^ $(LIBS@asfx@)

endif

@adir@/fresample: $(tool_obj@asfx@) @adir@/libfresample.a
\t$(CC@asfx@) -o $@ $(LDFLAGS@asfx@) $^ $(LIBS@asfx@)
"""

def get_sources(dirpath):
    sources = {}
    for name in os.listdir(dirpath):
        if name.startswith('.'):
            continue
        base, ext = os.path.splitext(name)
        path = dirpath + '/' + name
        if ext == '.h':
            s = 'include'
        elif ext == '.c':
            s = get_special(base)
            if s is None:
                s = 'base'
        else:
            continue
        try:
            sources[s].append(path)
        except KeyError:
            sources[s] = [path]
    return sources

subst_vars = set('CC CCSHARED LDFLAGS LIBS CFLAGS CWARN CPPFLAGS'.split())

def build_sources(fp, name, sources, **kw):
    cflags = kw.get('cflags', '')
    prefix = kw.get('prefix', '')

    for s in ['base'] + ALL_SPECIAL:
        try:
            srclist = sources[s]
        except KeyError:
            continue

        if s == 'base':
            s_cflags = ''
        else:
            s_cflags = '$(%s_CFLAGS@asfx@)' % s.upper()
            subst_vars.add('%s_CFLAGS' % s.upper())
            subst_vars.add('%s_ENABLED' % s.upper())

        rule = ('\t$(CC@asfx@) -c -o $@ $< -I$(srcdir)/include $(depflags) '
                '$(CPPFLAGS@asfx@) %s %s $(CWARN@asfx@) $(CFLAGS@asfx@)\n' %
                (s_cflags, cflags))

        objlist = []
        for src in sorted(srclist):
            base = os.path.splitext(os.path.split(src)[1])[0]
            obj = '@adir@/' + prefix + base + '.o'
            src = '$(srcdir)/' + src
            objlist.append(obj)
            fp.write('%s: %s $(missing_dirs@asfx@)\n' % (obj, src))
            fp.write(rule)

        objlist = ' '.join(objlist)
        if s == 'base':
            fp.write('%s@asfx@ := %s\n' % (name, objlist))
        else:
            fp.write(
                'ifeq ($(%s_ENABLED@asfx@),1)\n'
                '%s@asfx@ += %s\n'
                'endif\n'
                % (s.upper(), name, objlist))

def run():
    fp = open('arch.mak.in', 'w')
    fp.write(HEADER)

    fp2 = StringIO()
    lib_src = get_sources('lib')
    tool_src = get_sources('src')
    build_sources(fp2, 'shared_obj', lib_src,
                  prefix='shared/', cflags='$(CCSHARED@asfx@)')
    fp2.write('\n')
    build_sources(fp2, 'static_obj', lib_src,
                  prefix='static/')
    fp2.write('\n')
    build_sources(fp2, 'tool_obj', tool_src, prefix='tool/')

    for var in sorted(subst_vars):
        fp.write('%s@asfx@ := @%s@\n' % (var, var))
    fp.write('\n')
    fp.write(fp2.getvalue())

    fp.write(FOOTER)
    fp.close()

run()
